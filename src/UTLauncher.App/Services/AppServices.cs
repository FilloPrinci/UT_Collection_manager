using Microsoft.Extensions.Logging;
using UTLauncher.Core.Download;
using UTLauncher.Core.Extraction;
using UTLauncher.Core.Games;
using UTLauncher.Core.InstallRegistry;
using UTLauncher.Core.Logging;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Platform;
using UTLauncher.Core.Processes;
using UTLauncher.Core.Tools;
using UTLauncher.Core.Updates;

namespace UTLauncher.App.Services;

/// <summary>
/// Composition root: constructs every Core service the UI needs once at startup. The app is
/// small enough that a DI container would be pure ceremony; view models just receive this.
/// </summary>
public sealed class AppServices : IDisposable
{
    public required IPlatform Platform { get; init; }
    public required LoggingSession LoggingSession { get; init; }
    public required ILoggerFactory LoggerFactory { get; init; }
    public required Manifest Manifest { get; init; }
    public required HttpClient HttpClient { get; init; }
    public required Downloader Downloader { get; init; }
    public required Iso9660Extractor IsoExtractor { get; init; }
    public required ArchiveExtractor ArchiveExtractor { get; init; }
    public required ProcessRunner ProcessRunner { get; init; }
    public required ToolManager ToolManager { get; init; }
    public required SystemLibraryLocator SystemLibraryLocator { get; init; }
    public required WindowsDependencyInstaller WindowsDependencyInstaller { get; init; }
    public required LinuxDependencyInstaller LinuxDependencyInstaller { get; init; }
    public required InstallationRegistry Registry { get; init; }
    public required InstallationVerifier Verifier { get; init; }
    public required UpdateChecker UpdateChecker { get; init; }
    public required ProtonManager ProtonManager { get; init; }
    public required UmuRunner UmuRunner { get; init; }

    public static async Task<AppServices> CreateAsync(bool verbose, CancellationToken cancellationToken)
    {
        var platform = PlatformResolver.Resolve(new SystemEnvironmentInfo());
        var loggingSession = LoggingSetup.Create(platform, verbose);
        var loggerFactory = loggingSession.Factory;

        var loader = new ManifestLoader();
        var manifest = await loader.LoadBundledAsync(cancellationToken).ConfigureAwait(false);

        var validation = ManifestValidator.Validate(manifest);
        var manifestLogger = loggerFactory.CreateLogger("Manifest");
        foreach (var warning in validation.Warnings)
        {
            manifestLogger.LogWarning("Manifest: {Warning}", warning);
        }

        foreach (var error in validation.Errors)
        {
            manifestLogger.LogError("Manifest: {Error}", error);
        }

        if (!validation.IsValid)
        {
            throw new ManifestLoadException(
                $"The bundled manifest failed validation ({validation.Errors.Count} error(s), see log).");
        }

        var httpClient = new HttpClient();
        var downloader = new Downloader(httpClient, loggerFactory.CreateLogger<Downloader>());
        var isoExtractor = new Iso9660Extractor();
        var archiveExtractor = new ArchiveExtractor();
        var processRunner = new ProcessRunner(loggerFactory.CreateLogger<ProcessRunner>());
        var toolManager = new ToolManager(downloader, platform);
        var systemLibraryLocator = new SystemLibraryLocator(processRunner);
        var windowsDependencyInstaller = new WindowsDependencyInstaller(downloader, processRunner, loggerFactory.CreateLogger<WindowsDependencyInstaller>());
        var linuxDependencyInstaller = new LinuxDependencyInstaller(systemLibraryLocator, processRunner, loggerFactory.CreateLogger<LinuxDependencyInstaller>());
        var registryPath = Path.Combine(platform.GetRootDirectory(), "installations.json");
        var registry = new InstallationRegistry(registryPath);
        var verifier = new InstallationVerifier(registry, platform);
        var updateChecker = new UpdateChecker(httpClient);
        var protonManager = new ProtonManager(downloader, platform);
        var umuRunner = new UmuRunner(toolManager, protonManager, processRunner);

        return new AppServices
        {
            Platform = platform,
            LoggingSession = loggingSession,
            LoggerFactory = loggerFactory,
            Manifest = manifest,
            HttpClient = httpClient,
            Downloader = downloader,
            IsoExtractor = isoExtractor,
            ArchiveExtractor = archiveExtractor,
            ProcessRunner = processRunner,
            ToolManager = toolManager,
            SystemLibraryLocator = systemLibraryLocator,
            WindowsDependencyInstaller = windowsDependencyInstaller,
            LinuxDependencyInstaller = linuxDependencyInstaller,
            Registry = registry,
            Verifier = verifier,
            UpdateChecker = updateChecker,
            ProtonManager = protonManager,
            UmuRunner = umuRunner,
        };
    }

    public string DefaultInstallPathFor(string gameShortName) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games", gameShortName);

    public void Dispose()
    {
        HttpClient.Dispose();
        LoggingSession.Dispose();
    }
}
