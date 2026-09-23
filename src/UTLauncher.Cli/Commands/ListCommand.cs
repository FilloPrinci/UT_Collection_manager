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
            logger.LogError(ex, "Impossibile caricare il manifest incorporato");
            Console.Error.WriteLine($"Errore: {ex.Message}");
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

        Console.WriteLine($"Manifest v{manifest.ManifestVersion} (aggiornato: {manifest.Updated})");
        Console.WriteLine();
        Console.WriteLine($"{"ID",-10} {"Versione",-20} Nome");
        foreach (var game in manifest.Games)
        {
            Console.WriteLine($"{game.Id,-10} {game.VersionCode,-20} {game.Name}");
        }

        if (validation.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"{validation.Warnings.Count} avviso/i (usa --verbose o consulta il log per i dettagli).");
        }

        if (!validation.IsValid)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"{validation.Errors.Count} errore/i nel manifest:");
            foreach (var error in validation.Errors)
            {
                Console.Error.WriteLine($"  - {error}");
            }

            return 1;
        }

        return 0;
    }
}
