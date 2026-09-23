using Microsoft.Extensions.Logging;
using UTLauncher.Core.Manifest;

namespace UTLauncher.Cli.Commands;

public static class ListCommand
{
    public static async Task<int> RunAsync(ILogger logger, CancellationToken cancellationToken)
    {
        var loader = new ManifestLoader();
        Core.Manifest.Manifest manifest;

        try
        {
            manifest = await loader.LoadBundledAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ManifestLoadException ex)
        {
            logger.LogError(ex, "Could not load the embedded manifest");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        var validation = ManifestValidator.Validate(manifest);

        foreach (var warning in validation.Warnings)
        {
            logger.LogWarning("Manifest: {Warning}", warning);
        }

        foreach (var error in validation.Errors)
        {
            logger.LogError("Manifest: {Error}", error);
        }

        Console.WriteLine($"Manifest v{manifest.ManifestVersion} (updated: {manifest.Updated})");
        Console.WriteLine();
        Console.WriteLine($"{"ID",-10} {"Version",-20} Name");
        foreach (var game in manifest.Games)
        {
            Console.WriteLine($"{game.Id,-10} {game.VersionCode,-20} {game.Name}");
        }

        if (validation.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"{validation.Warnings.Count} warning(s) (use --verbose or check the log for details).");
        }

        if (!validation.IsValid)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"{validation.Errors.Count} error(s) in the manifest:");
            foreach (var error in validation.Errors)
            {
                Console.Error.WriteLine($"  - {error}");
            }

            return 1;
        }

        return 0;
    }
}
