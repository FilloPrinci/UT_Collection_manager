using Microsoft.Extensions.Logging;
using UTLauncher.Core.Manifest;

namespace UTLauncher.Cli;

public static class ManifestBootstrap
{
    /// <summary>
    /// Loads the bundled manifest and validates it, printing errors and returning a null
    /// manifest on failure so callers can just check for null and return the exit code.
    /// </summary>
    public static async Task<Manifest?> LoadValidatedAsync(ILogger logger, CancellationToken cancellationToken)
    {
        var loader = new ManifestLoader();
        Manifest manifest;
        try
        {
            manifest = await loader.LoadBundledAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ManifestLoadException ex)
        {
            logger.LogError(ex, "Could not load the embedded manifest");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return null;
        }

        var validation = ManifestValidator.Validate(manifest);
        if (!validation.IsValid)
        {
            Console.Error.WriteLine("Manifest failed validation:");
            foreach (var error in validation.Errors)
            {
                Console.Error.WriteLine($"  - {error}");
            }

            return null;
        }

        return manifest;
    }
}
