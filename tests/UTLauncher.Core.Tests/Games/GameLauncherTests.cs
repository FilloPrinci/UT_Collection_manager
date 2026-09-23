using Microsoft.Extensions.Logging.Abstractions;
using UTLauncher.Core.Games;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Processes;
using CorePlatform = UTLauncher.Core.Platform;

namespace UTLauncher.Core.Tests.Games;

public class GameLauncherTests
{
    private sealed class FakePlatform(string id) : CorePlatform.IPlatform
    {
        public string Id => id;
        public string GetRootDirectory() => Path.GetTempPath();
        public string GetLogDirectory() => Path.GetTempPath();
    }

    private static GameEntry MakeGame(IReadOnlyDictionary<string, LaunchEntry>? launch) => new(
        Id: "ut99",
        Name: "Unreal Tournament (GOTY)",
        VersionCode: "UT99-469e",
        Reference: null,
        Sources: new Dictionary<string, SourceFile>(),
        Patch: null,
        Launch: launch,
        Network: null,
        DiskSpaceRequiredBytes: null,
        MasterServer: null,
        Ut4uuInstallInfo: null,
        AccountRegistrationUrl: null,
        Dependencies: null,
        CdKey: null);

    private static LaunchEntry MakeLaunchEntry(string exe) =>
        new(Exe: exe, Args: "", WorkingDir: null, Notes: null, Runner: null, WindowsInstallPath: null, Winetricks: null);

    private static GameLauncher CreateLauncher(string platformId) =>
        new(new ProcessRunner(NullLogger<ProcessRunner>.Instance), new FakePlatform(platformId), NullLogger<GameLauncher>.Instance);

    [Fact]
    public async Task LaunchAsync_RunsTheExecutableResolvedForTheCurrentPlatform()
    {
        // Spawns a real /bin/sh script, like ProcessRunnerTests: Unix-only (Linux VM is the
        // first-phase test target, see CLAUDE.md).
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var installPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var systemDirectory = Path.Combine(installPath, "System64");
        Directory.CreateDirectory(systemDirectory);
        var exePath = Path.Combine(systemDirectory, "ut-bin");
        await File.WriteAllTextAsync(exePath, "#!/bin/sh\nexit 42\n");
        File.SetUnixFileMode(exePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        try
        {
            var game = MakeGame(new Dictionary<string, LaunchEntry> { ["linux-x64"] = MakeLaunchEntry("System64/ut-bin") });
            var launcher = CreateLauncher("linux-x64");

            var result = await launcher.LaunchAsync(game, installPath, CancellationToken.None);

            Assert.Equal(42, result.ExitCode);
        }
        finally
        {
            Directory.Delete(installPath, recursive: true);
        }
    }

    [Fact]
    public async Task LaunchAsync_Throws_WhenManifestHasNoLaunchEntryForThePlatform()
    {
        var game = MakeGame(new Dictionary<string, LaunchEntry> { ["windows"] = MakeLaunchEntry("System/UnrealTournament.exe") });
        var launcher = CreateLauncher("linux-x64");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => launcher.LaunchAsync(game, Path.GetTempPath(), CancellationToken.None));
    }

    [Fact]
    public async Task LaunchAsync_Throws_WhenExecutableIsMissingOnDisk()
    {
        var installPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(installPath);

        try
        {
            var game = MakeGame(new Dictionary<string, LaunchEntry> { ["linux-x64"] = MakeLaunchEntry("System64/ut-bin") });
            var launcher = CreateLauncher("linux-x64");

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => launcher.LaunchAsync(game, installPath, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(installPath, recursive: true);
        }
    }
}
