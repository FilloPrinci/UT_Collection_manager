using Microsoft.Extensions.Logging;
using UTLauncher.Core.Download;
using UTLauncher.Core.Extraction;
using UTLauncher.Core.Games;
using UTLauncher.Core.InstallRegistry;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Platform;
using UTLauncher.Core.Processes;
using UTLauncher.Core.Tools;

namespace UTLauncher.Cli.Commands;

public static class InstallCommand
{
    private static readonly HashSet<string> SupportedGameIds = ["ut99", "ut2004"];

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        IPlatform platform,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("UTLauncher.Cli.Install");

        if (args.Count == 0)
        {
            Console.Error.WriteLine("Usage: utlauncher install <ut99|ut2004|ut4> --dest <path> [--verbose]");
            return 1;
        }

        var gameId = args[0];
        string? destination = null;
        for (var i = 1; i < args.Count; i++)
        {
            if (args[i] == "--dest" && i + 1 < args.Count)
            {
                destination = args[i + 1];
                i++;
            }
        }

        if (string.IsNullOrWhiteSpace(destination))
        {
            Console.Error.WriteLine("Missing required --dest <path> argument.");
            return 1;
        }

        if (!SupportedGameIds.Contains(gameId))
        {
            Console.Error.WriteLine($"Install for '{gameId}' is not implemented yet (coming in a later step of the plan).");
            return 1;
        }

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
        if (!validation.IsValid)
        {
            Console.Error.WriteLine("Manifest failed validation, aborting install:");
            foreach (var error in validation.Errors)
            {
                Console.Error.WriteLine($"  - {error}");
            }

            return 1;
        }

        var game = manifest.Games.FirstOrDefault(g => g.Id == gameId);
        if (game is null)
        {
            Console.Error.WriteLine($"Unknown game id in manifest: '{gameId}'.");
            return 1;
        }

        using var httpClient = new HttpClient();
        var downloader = new Downloader(httpClient, loggerFactory.CreateLogger<Downloader>());
        var isoExtractor = new Iso9660Extractor();
        var archiveExtractor = new ArchiveExtractor();
        var processRunner = new ProcessRunner(loggerFactory.CreateLogger<ProcessRunner>());
        var registryPath = Path.Combine(platform.GetRootDirectory(), "installations.json");
        var registry = new InstallationRegistry(registryPath);
        var progress = new ConsoleProgressReporter();

        try
        {
            InstallationRecord record;
            if (gameId == "ut2004")
            {
                var toolManager = new ToolManager(downloader, platform);
                var installer = new Ut2004Installer(
                    downloader,
                    isoExtractor,
                    archiveExtractor,
                    processRunner,
                    toolManager,
                    registry,
                    platform,
                    loggerFactory.CreateLogger<Ut2004Installer>());
                record = await installer.InstallAsync(manifest, game, destination, progress, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                var installer = new Ut99Installer(
                    downloader,
                    isoExtractor,
                    archiveExtractor,
                    processRunner,
                    registry,
                    platform,
                    loggerFactory.CreateLogger<Ut99Installer>());
                record = await installer.InstallAsync(game, destination, progress, cancellationToken)
                    .ConfigureAwait(false);
            }

            Console.WriteLine();
            Console.WriteLine($"Installed {game.Name} ({record.VersionCode}) to {record.InstallPath}");
            return 0;
        }
        catch (Exception ex) when (ex is DownloadException or InvalidOperationException or ToolNotConfiguredException)
        {
            logger.LogError(ex, "Installation of {GameId} failed", gameId);
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Error: {ex.Message}");

            if (ex.InnerException is HashMismatchException hashMismatch)
            {
                Console.Error.WriteLine($"  expected sha256: {hashMismatch.ExpectedSha256}");
                Console.Error.WriteLine($"  actual sha256:   {hashMismatch.ActualSha256}");
            }

            return 1;
        }
    }
}
