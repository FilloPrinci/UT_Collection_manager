using Microsoft.Extensions.Logging;
using UTLauncher.Core.Games;
using UTLauncher.Core.InstallRegistry;
using UTLauncher.Core.Platform;

namespace UTLauncher.Cli.Commands;

public static class VerifyCommand
{
    public static async Task<int> RunAsync(
        string gameId,
        IPlatform platform,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("UTLauncher.Cli.Verify");

        var manifest = await ManifestBootstrap.LoadValidatedAsync(logger, cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            return 1;
        }

        var game = manifest.Games.FirstOrDefault(g => g.Id == gameId);
        if (game is null)
        {
            Console.Error.WriteLine($"Unknown game id in manifest: '{gameId}'.");
            return 1;
        }

        var registryPath = Path.Combine(platform.GetRootDirectory(), "installations.json");
        var registry = new InstallationRegistry(registryPath);
        var verifier = new InstallationVerifier(registry, platform);

        var result = await verifier.VerifyAsync(game, cancellationToken).ConfigureAwait(false);

        if (result.IsValid)
        {
            Console.WriteLine($"'{game.Name}' is correctly installed.");
            return 0;
        }

        Console.Error.WriteLine($"Verification failed for '{game.Name}':");
        foreach (var issue in result.Issues)
        {
            Console.Error.WriteLine($"  - {issue}");
        }

        return 1;
    }
}
