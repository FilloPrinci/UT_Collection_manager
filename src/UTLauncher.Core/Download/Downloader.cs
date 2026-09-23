using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using UTLauncher.Core.Hashing;
using UTLauncher.Core.Tasks;

namespace UTLauncher.Core.Download;

public sealed class Downloader(HttpClient httpClient, ILogger<Downloader> logger)
{
    private const int BufferSize = 81920;
    private static readonly TimeSpan ProgressReportInterval = TimeSpan.FromMilliseconds(200);

    public async Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (request.Urls.Count == 0)
        {
            throw new DownloadException($"No URL available for '{request.DestinationPath}'.");
        }

        var destinationDirectory = Path.GetDirectoryName(Path.GetFullPath(request.DestinationPath));
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var existing = await TryUseExistingFileAsync(request, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            logger.LogInformation("File already present and verified: {Path}", request.DestinationPath);
            return existing;
        }

        var partPath = request.DestinationPath + ".part";
        Exception? lastError = null;

        foreach (var url in request.Urls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var resumed = await DownloadFromUrlAsync(url, partPath, request, progress, cancellationToken)
                    .ConfigureAwait(false);

                progress?.Report(TaskProgress.Indeterminate($"Verifying hash of {Path.GetFileName(request.DestinationPath)}"));
                var hashResult = await HashCalculator.ComputeFileAsync(partPath, progress: null, cancellationToken)
                    .ConfigureAwait(false);

                if (!HashCalculator.Matches(hashResult.Sha256Hex, request.ExpectedSha256))
                {
                    logger.LogError(
                        "Hash mismatch for {Url}: expected {Expected}, got {Actual}",
                        url,
                        request.ExpectedSha256,
                        hashResult.Sha256Hex);
                    File.Delete(partPath);
                    lastError = new HashMismatchException(
                        $"Hash mismatch for '{request.DestinationPath}' downloaded from '{url}'.",
                        request.ExpectedSha256,
                        hashResult.Sha256Hex);
                    continue;
                }

                File.Move(partPath, request.DestinationPath, overwrite: true);
                logger.LogInformation(
                    "Download completed and verified: {Path} ({Size} bytes)",
                    request.DestinationPath,
                    hashResult.Size);
                return new DownloadResult(request.DestinationPath, hashResult.Size, hashResult.Sha256Hex, resumed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                logger.LogWarning(ex, "Download failed from {Url}, trying next mirror if available", url);
                lastError = ex;
            }
        }

        throw new DownloadException($"All mirrors failed for '{request.DestinationPath}'.", lastError);
    }

    private async Task<DownloadResult?> TryUseExistingFileAsync(DownloadRequest request, CancellationToken cancellationToken)
    {
        if (!File.Exists(request.DestinationPath))
        {
            return null;
        }

        if (request.ExpectedSize is { } expectedSize && new FileInfo(request.DestinationPath).Length != expectedSize)
        {
            logger.LogWarning(
                "Existing file has a different size than expected, it will be re-downloaded: {Path}",
                request.DestinationPath);
            File.Delete(request.DestinationPath);
            return null;
        }

        var hashResult = await HashCalculator.ComputeFileAsync(request.DestinationPath, progress: null, cancellationToken)
            .ConfigureAwait(false);

        if (!HashCalculator.Matches(hashResult.Sha256Hex, request.ExpectedSha256))
        {
            logger.LogWarning(
                "Existing file does not match the expected hash, it will be re-downloaded: {Path}",
                request.DestinationPath);
            File.Delete(request.DestinationPath);
            return null;
        }

        return new DownloadResult(request.DestinationPath, hashResult.Size, hashResult.Sha256Hex, ResumedFromPartial: false);
    }

    private async Task<bool> DownloadFromUrlAsync(
        string url,
        string partPath,
        DownloadRequest request,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        var existingLength = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
        if (existingLength > 0)
        {
            requestMessage.Headers.Range = new RangeHeaderValue(existingLength, null);
        }

        using var response = await httpClient
            .SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var resumed = existingLength > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (existingLength > 0 && !resumed)
        {
            logger.LogInformation("Server does not support resume for {Url}, starting over from scratch", url);
            existingLength = 0;
        }

        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        var totalBytes = contentLength.HasValue ? contentLength.Value + existingLength : (long?)null;

        var fileName = Path.GetFileName(request.DestinationPath);

        await using var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = new FileStream(
            partPath,
            resumed ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        var buffer = new byte[BufferSize];
        long transferred = existingLength;
        var stopwatch = Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;
        int read;

        while ((read = await httpStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            transferred += read;

            var elapsed = stopwatch.Elapsed;
            var isLastChunk = totalBytes.HasValue && transferred >= totalBytes.Value;
            if (elapsed - lastReport >= ProgressReportInterval || isLastChunk)
            {
                lastReport = elapsed;
                var elapsedSeconds = elapsed.TotalSeconds;
                var bytesPerSecond = elapsedSeconds > 0 ? (transferred - existingLength) / elapsedSeconds : 0;
                TimeSpan? eta = totalBytes.HasValue && bytesPerSecond > 0
                    ? TimeSpan.FromSeconds((totalBytes.Value - transferred) / bytesPerSecond)
                    : null;

                progress?.Report(totalBytes.HasValue
                    ? TaskProgress.Determinate($"Downloading {fileName}", transferred, totalBytes.Value, bytesPerSecond, eta)
                    : TaskProgress.Indeterminate($"Downloading {fileName}"));
            }
        }

        return resumed;
    }
}
