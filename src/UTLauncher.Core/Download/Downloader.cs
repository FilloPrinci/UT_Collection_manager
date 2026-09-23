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
            throw new DownloadException($"Nessun URL disponibile per '{request.DestinationPath}'.");
        }

        var destinationDirectory = Path.GetDirectoryName(Path.GetFullPath(request.DestinationPath));
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var existing = await TryUseExistingFileAsync(request, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            logger.LogInformation("File già presente e verificato: {Path}", request.DestinationPath);
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

                progress?.Report(TaskProgress.Indeterminate($"Verifica hash di {Path.GetFileName(request.DestinationPath)}"));
                var hashResult = await HashCalculator.ComputeFileAsync(partPath, progress: null, cancellationToken)
                    .ConfigureAwait(false);

                if (!HashCalculator.Matches(hashResult.Sha256Hex, request.ExpectedSha256))
                {
                    logger.LogError(
                        "Hash non corrispondente per {Url}: atteso {Expected}, ottenuto {Actual}",
                        url,
                        request.ExpectedSha256,
                        hashResult.Sha256Hex);
                    File.Delete(partPath);
                    lastError = new HashMismatchException(
                        $"Hash non corrispondente per '{request.DestinationPath}' scaricato da '{url}'.",
                        request.ExpectedSha256,
                        hashResult.Sha256Hex);
                    continue;
                }

                File.Move(partPath, request.DestinationPath, overwrite: true);
                logger.LogInformation(
                    "Download completato e verificato: {Path} ({Size} byte)",
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
                logger.LogWarning(ex, "Download fallito da {Url}, provo il mirror successivo se disponibile", url);
                lastError = ex;
            }
        }

        throw new DownloadException($"Tutti i mirror sono falliti per '{request.DestinationPath}'.", lastError);
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
                "File esistente ha dimensione diversa da quella attesa, verrà riscaricato: {Path}",
                request.DestinationPath);
            File.Delete(request.DestinationPath);
            return null;
        }

        var hashResult = await HashCalculator.ComputeFileAsync(request.DestinationPath, progress: null, cancellationToken)
            .ConfigureAwait(false);

        if (!HashCalculator.Matches(hashResult.Sha256Hex, request.ExpectedSha256))
        {
            logger.LogWarning(
                "File esistente non corrisponde all'hash atteso, verrà riscaricato: {Path}",
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
            logger.LogInformation("Il server non supporta la ripresa per {Url}, riparto da zero", url);
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
                    ? TaskProgress.Determinate($"Download {fileName}", transferred, totalBytes.Value, bytesPerSecond, eta)
                    : TaskProgress.Indeterminate($"Download {fileName}"));
            }
        }

        return resumed;
    }
}
