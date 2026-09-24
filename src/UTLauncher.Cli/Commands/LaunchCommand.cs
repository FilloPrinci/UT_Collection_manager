using Microsoft.Extensions.Logging;
using UTLauncher.Core.Download;
using UTLauncher.Core.Games;
using UTLauncher.Core.InstallRegistry;
using UTLauncher.Core.Platform;
using UTLauncher.Core.Processes;
using UTLauncher.Core.Tools;

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
        using var httpClient = new HttpClient();
        var downloader = new Downloader(httpClient, loggerFactory.CreateLogger<Downloader>());
        var toolManager = new ToolManager(downloader, platform);
        var protonManager = new ProtonManager(downloader, platform);
        var umuRunner = new UmuRunner(toolManager, protonManager, processRunner);
        var launcher = new GameLauncher(processRunner, registry, umuRunner, platform, loggerFactory.CreateLogger<GameLauncher>());

        var result = await launcher.LaunchAsync(manifest, game, record.InstallPath, cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }
}
