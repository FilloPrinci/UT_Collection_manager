using Microsoft.Extensions.Logging;
using UTLauncher.Core.Games;
using UTLauncher.Core.InstallRegistry;
using UTLauncher.Core.Platform;
using UTLauncher.Core.Processes;

namespace UTLauncher.Cli.Commands;

public static class LaunchCommand
{
    public static async Task<int> RunAsync(
        string gameId,
        IPlatform platform,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("UTLauncher.Cli.Launch");

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
        var record = await registry.GetAsync(gameId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            Console.Error.WriteLine($"'{game.Name}' is not installed.");
            return 1;
        }

        var processRunner = new ProcessRunner(loggerFactory.CreateLogger<ProcessRunner>());
        var launcher = new GameLauncher(processRunner, platform, loggerFactory.CreateLogger<GameLauncher>());

        var result = await launcher.LaunchAsync(game, record.InstallPath, cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }
}
