using System.Security.Cryptography;

namespace UTLauncher.Core.Hashing;

public sealed record HashResult(long Size, string Sha256Hex);

public static class HashCalculator
{
    private const int BufferSize = 81920;

    public static async Task<HashResult> ComputeAsync(Stream stream, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[BufferSize];
        long total = 0;
        int read;

        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            incrementalHash.AppendData(buffer, 0, read);
            total += read;
            progress?.Report(total);
        }

        var hash = incrementalHash.GetHashAndReset();
        return new HashResult(total, Convert.ToHexStringLower(hash));
    }

    public static async Task<HashResult> ComputeFileAsync(string path, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await ComputeAsync(stream, progress, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<HashResult> ComputeUrlAsync(HttpClient httpClient, string url, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        using var response = await httpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await ComputeAsync(stream, progress, cancellationToken).ConfigureAwait(false);
    }

    public static bool Matches(string sha256Hex, string expectedSha256Hex) =>
        string.Equals(sha256Hex, expectedSha256Hex, StringComparison.OrdinalIgnoreCase);
}
