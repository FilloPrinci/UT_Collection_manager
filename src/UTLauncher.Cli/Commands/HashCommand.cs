using Microsoft.Extensions.Logging;
using UTLauncher.Core.Hashing;

namespace UTLauncher.Cli.Commands;

public static class HashCommand
{
    public static async Task<int> RunAsync(string target, ILogger logger, CancellationToken cancellationToken)
    {
        var isUrl = Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        logger.LogInformation("Calcolo hash di {Target} ({Kind})", target, isUrl ? "URL" : "file locale");

        try
        {
            HashResult result;
            if (isUrl)
            {
                using var httpClient = new HttpClient();
                result = await HashCalculator.ComputeUrlAsync(httpClient, target, progress: null, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                if (!File.Exists(target))
                {
                    Console.Error.WriteLine($"Errore: file non trovato: '{target}'.");
                    return 1;
                }

                result = await HashCalculator.ComputeFileAsync(target, progress: null, cancellationToken)
                    .ConfigureAwait(false);
            }

            Console.WriteLine($"size:   {result.Size}");
            Console.WriteLine($"sha256: {result.Sha256Hex}");
            return 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Calcolo hash fallito per {Target}", target);
            Console.Error.WriteLine($"Errore: {ex.Message}");
            return 1;
        }
    }
}
