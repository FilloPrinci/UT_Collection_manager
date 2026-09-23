using UTLauncher.Core.Games;
using UTLauncher.Core.InstallRegistry;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Tests.Platform;
using CorePlatform = UTLauncher.Core.Platform;

namespace UTLauncher.Core.Tests.Games;

public class InstallationVerifierTests
{
    private static GameEntry MakeGame(string versionCode = "UT99-469e", string exe = "System64/ut-bin") => new(
        Id: "ut99",
        Name: "Unreal Tournament (GOTY)",
        VersionCode: versionCode,
        Reference: null,
        Sources: new Dictionary<string, SourceFile>(),
        Patch: null,
        Launch: new Dictionary<string, LaunchEntry>
        {
            ["linux-x64"] = new LaunchEntry(Exe: exe, Args: "", WorkingDir: null, Notes: null, Runner: null, WindowsInstallPath: null, Winetricks: null),
        },
        Network: null,
        DiskSpaceRequiredBytes: null,
        MasterServer: null,
        Ut4uuInstallInfo: null,
        AccountRegistrationUrl: null,
        Dependencies: null,
        CdKey: null);

    private static CorePlatform.IPlatform MakeLinuxPlatform(string rootDir)
    {
        var env = new FakeEnvironmentInfo().SetVariable("XDG_DATA_HOME", rootDir);
        return new CorePlatform.LinuxPlatform(env);
    }

    private static string CreateInstalledGame(string exeRelativePath)
    {
        var installPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var exePath = Path.Combine(installPath, exeRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(exePath)!);
        File.WriteAllText(exePath, "fake binary");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(exePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return installPath;
    }

    [Fact]
    public async Task VerifyAsync_ReturnsInvalid_WhenNotRegistered()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var platform = MakeLinuxPlatform(rootDir);
        var registry = new InstallationRegistry(Path.Combine(rootDir, "installations.json"));
        var verifier = new InstallationVerifier(registry, platform);

        var result = await verifier.VerifyAsync(MakeGame(), CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Contains("not registered"));
    }

    [Fact]
    public async Task VerifyAsync_ReturnsValid_ForACorrectFreshInstall()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var platform = MakeLinuxPlatform(rootDir);
        var registry = new InstallationRegistry(Path.Combine(rootDir, "installations.json"));
        var installPath = CreateInstalledGame("System64/ut-bin");

        try
        {
            var game = MakeGame();
            await registry.UpsertAsync(
                new InstallationRecord("ut99", installPath, game.VersionCode, platform.Id, new Dictionary<string, string>(), DateTimeOffset.UtcNow),
                CancellationToken.None);

            var verifier = new InstallationVerifier(registry, platform);
            var result = await verifier.VerifyAsync(game, CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Empty(result.Issues);
        }
        finally
        {
            Directory.Delete(installPath, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_ReturnsInvalid_WhenInstallPathMissing()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var platform = MakeLinuxPlatform(rootDir);
        var registry = new InstallationRegistry(Path.Combine(rootDir, "installations.json"));
        var game = MakeGame();
        var missingPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        await registry.UpsertAsync(
            new InstallationRecord("ut99", missingPath, game.VersionCode, platform.Id, new Dictionary<string, string>(), DateTimeOffset.UtcNow),
            CancellationToken.None);

        var verifier = new InstallationVerifier(registry, platform);
        var result = await verifier.VerifyAsync(game, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Contains("does not exist"));
    }

    [Fact]
    public async Task VerifyAsync_ReportsIssue_WhenVersionCodeDiffersFromManifest()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var platform = MakeLinuxPlatform(rootDir);
        var registry = new InstallationRegistry(Path.Combine(rootDir, "installations.json"));
        var installPath = CreateInstalledGame("System64/ut-bin");

        try
        {
            var game = MakeGame(versionCode: "UT99-469e");
            await registry.UpsertAsync(
                new InstallationRecord("ut99", installPath, "UT99-OLDVERSION", platform.Id, new Dictionary<string, string>(), DateTimeOffset.UtcNow),
                CancellationToken.None);

            var verifier = new InstallationVerifier(registry, platform);
            var result = await verifier.VerifyAsync(game, CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains(result.Issues, i => i.Contains("differs from the manifest"));
        }
        finally
        {
            Directory.Delete(installPath, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_ReportsIssue_WhenLaunchExecutableMissing()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var platform = MakeLinuxPlatform(rootDir);
        var registry = new InstallationRegistry(Path.Combine(rootDir, "installations.json"));
        var installPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(installPath);

        try
        {
            var game = MakeGame();
            await registry.UpsertAsync(
                new InstallationRecord("ut99", installPath, game.VersionCode, platform.Id, new Dictionary<string, string>(), DateTimeOffset.UtcNow),
                CancellationToken.None);

            var verifier = new InstallationVerifier(registry, platform);
            var result = await verifier.VerifyAsync(game, CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains(result.Issues, i => i.Contains("Launch executable not found"));
        }
        finally
        {
            Directory.Delete(installPath, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_ReportsIssue_WhenLaunchExecutableNotExecutable()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var platform = MakeLinuxPlatform(rootDir);
        var registry = new InstallationRegistry(Path.Combine(rootDir, "installations.json"));
        var installPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var exePath = Path.Combine(installPath, "System64", "ut-bin");
        Directory.CreateDirectory(Path.GetDirectoryName(exePath)!);
        File.WriteAllText(exePath, "not executable");
        File.SetUnixFileMode(exePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        try
        {
            var game = MakeGame();
            await registry.UpsertAsync(
                new InstallationRecord("ut99", installPath, game.VersionCode, platform.Id, new Dictionary<string, string>(), DateTimeOffset.UtcNow),
                CancellationToken.None);

            var verifier = new InstallationVerifier(registry, platform);
            var result = await verifier.VerifyAsync(game, CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains(result.Issues, i => i.Contains("not marked executable"));
        }
        finally
        {
            Directory.Delete(installPath, recursive: true);
        }
    }
}
