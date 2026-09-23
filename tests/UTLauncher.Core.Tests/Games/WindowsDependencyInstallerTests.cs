using Microsoft.Extensions.Logging.Abstractions;
using UTLauncher.Core.Download;
using UTLauncher.Core.Games;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Processes;
using UTLauncher.Core.Tests.Download;

namespace UTLauncher.Core.Tests.Games;

public class WindowsDependencyInstallerTests
{
    private static ProcessRunner MakeProcessRunner() => new(NullLogger<ProcessRunner>.Instance);

    private static GameEntry MakeGame(WindowsDependencies dependencies) => new(
        Id: "ut99",
        Name: "Unreal Tournament (GOTY)",
        VersionCode: "v1",
        Reference: null,
        Sources: new Dictionary<string, SourceFile>(),
        Patch: null,
        Launch: null,
        Network: null,
        DiskSpaceRequiredBytes: null,
        MasterServer: null,
        Ut4uuInstallInfo: null,
        AccountRegistrationUrl: null,
        Dependencies: new DependenciesSection(dependencies),
        CdKey: null);

    // WindowsDependencyInstaller only ever does anything on Windows (registry reads and elevated
    // installers are meaningless elsewhere); on Linux/macOS it must be a complete no-op even when
    // the manifest configures dependencies, which this proves by failing the test if a download
    // is ever attempted.
    [Fact]
    public async Task EnsureInstalledAsync_IsANoOp_OnNonWindowsPlatforms()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var handler = new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No download should be attempted on a non-Windows platform."));
        var downloader = new Downloader(new HttpClient(handler), NullLogger<Downloader>.Instance);
        var installer = new WindowsDependencyInstaller(downloader, MakeProcessRunner(), NullLogger<WindowsDependencyInstaller>.Instance);

        var vcRedistX86 = new VcRedistInstaller(
            "SOFTWARE\\Microsoft\\VisualStudio\\14.0\\VC\\Runtimes\\x86", "Installed",
            "vc_redist.x86.exe", "https://example.com/vc_redist.x86.exe", new string('a', 64), null,
            "/install /passive /norestart");
        var directXWebSetup = new DirectXWebSetupInstaller(
            "dxwebsetup.exe", "https://example.com/dxwebsetup.exe", new string('b', 64), null, "/q");
        var game = MakeGame(new WindowsDependencies(null, null, vcRedistX86, null, directXWebSetup));

        await installer.EnsureInstalledAsync(game, Path.GetTempPath(), progress: null, CancellationToken.None);
    }

    [Fact]
    public async Task EnsureInstalledAsync_IsANoOp_WhenGameHasNoWindowsDependenciesConfigured()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No download should be attempted without configured dependencies."));
        var downloader = new Downloader(new HttpClient(handler), NullLogger<Downloader>.Instance);
        var installer = new WindowsDependencyInstaller(downloader, MakeProcessRunner(), NullLogger<WindowsDependencyInstaller>.Instance);

        var game = new GameEntry(
            Id: "ut2004",
            Name: "Unreal Tournament 2004",
            VersionCode: "v1",
            Reference: null,
            Sources: new Dictionary<string, SourceFile>(),
            Patch: null,
            Launch: null,
            Network: null,
            DiskSpaceRequiredBytes: null,
            MasterServer: null,
            Ut4uuInstallInfo: null,
            AccountRegistrationUrl: null,
            Dependencies: null,
            CdKey: null);

        await installer.EnsureInstalledAsync(game, Path.GetTempPath(), progress: null, CancellationToken.None);
    }
}
