using UTLauncher.Core.Games;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Tests.Platform;
using CorePlatform = UTLauncher.Core.Platform;

namespace UTLauncher.Core.Tests.Games;

public class Ut99KeyBindingHelperTests
{
    [Fact]
    public void ApplyBindings_ReplacesConflictingDefault_ForS()
    {
        var lines = new List<string>
        {
            "[Engine.Input]",
            "Aliases[0]=(Command=\"Button bFire | Fire\",Alias=Fire)",
            "Shift=Walking",
            "S=Axis aUp Speed=+300.0",
            "T=Talk",
            "[Engine.OtherSection]",
            "Foo=Bar",
        };

        Ut99KeyBindingHelper.ApplyBindings(lines);

        Assert.Contains("W=MoveForward", lines);
        Assert.Contains("A=StrafeLeft", lines);
        Assert.Contains("S=MoveBackward", lines);
        Assert.Contains("D=StrafeRight", lines);
        Assert.DoesNotContain("S=Axis aUp Speed=+300.0", lines);
    }

    [Fact]
    public void ApplyBindings_DoesNotTouchOtherSections()
    {
        var lines = new List<string>
        {
            "[Engine.Input]",
            "S=Axis aUp Speed=+300.0",
            "[Engine.OtherSection]",
            "S=SomethingUnrelated",
        };

        Ut99KeyBindingHelper.ApplyBindings(lines);

        Assert.Equal("S=SomethingUnrelated", lines[^1]);
    }

    [Fact]
    public void ApplyBindings_InsertsMissingKeys_WithoutDuplicating()
    {
        var lines = new List<string>
        {
            "[Engine.Input]",
            "T=Talk",
        };

        Ut99KeyBindingHelper.ApplyBindings(lines);

        Assert.Single(lines, l => l == "W=MoveForward");
        Assert.Single(lines, l => l == "A=StrafeLeft");
        Assert.Single(lines, l => l == "S=MoveBackward");
        Assert.Single(lines, l => l == "D=StrafeRight");
    }

    [Fact]
    public void ApplyBindings_IsIdempotent()
    {
        var lines = new List<string> { "[Engine.Input]", "S=Axis aUp Speed=+300.0" };

        Ut99KeyBindingHelper.ApplyBindings(lines);
        Ut99KeyBindingHelper.ApplyBindings(lines);

        Assert.Single(lines, l => l == "S=MoveBackward");
        Assert.Single(lines, l => l == "W=MoveForward");
    }

    [Fact]
    public void ApplyBindings_Throws_WhenSectionMissing()
    {
        var lines = new List<string> { "[Other]" };

        Assert.Throws<InvalidOperationException>(() => Ut99KeyBindingHelper.ApplyBindings(lines));
    }

    private static GameEntry MakeGame() => new(
        Id: "ut99",
        Name: "Unreal Tournament (GOTY)",
        VersionCode: "UT99-469e",
        Reference: null,
        Sources: new Dictionary<string, SourceFile>(),
        Patch: null,
        Launch: new Dictionary<string, LaunchEntry>
        {
            ["linux-x64"] = new LaunchEntry(Exe: "System64/ut-bin", Args: "", WorkingDir: null, Notes: null, Runner: null, WindowsInstallPath: null, Winetricks: null),
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

    [Fact]
    public async Task ApplyWasdMovementAsync_EditsExistingUserIni()
    {
        var installPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var systemDir = Path.Combine(installPath, "System64");
        Directory.CreateDirectory(systemDir);
        await File.WriteAllLinesAsync(
            Path.Combine(systemDir, "User.ini"),
            ["[Engine.Input]", "S=Axis aUp Speed=+300.0", "T=Talk"]);

        try
        {
            var platform = MakeLinuxPlatform(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            var helper = new Ut99KeyBindingHelper();

            await helper.ApplyWasdMovementAsync(MakeGame(), platform, installPath, CancellationToken.None);

            var result = await File.ReadAllLinesAsync(Path.Combine(systemDir, "User.ini"));
            Assert.Contains("S=MoveBackward", result);
            Assert.Contains("W=MoveForward", result);
            Assert.Contains("T=Talk", result);
        }
        finally
        {
            Directory.Delete(installPath, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyWasdMovementAsync_CreatesUserIniFromDefault_WhenMissing()
    {
        var installPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var systemDir = Path.Combine(installPath, "System64");
        Directory.CreateDirectory(systemDir);
        await File.WriteAllLinesAsync(
            Path.Combine(systemDir, "DefUser.ini"),
            ["[Engine.Input]", "S=Axis aUp Speed=+300.0"]);

        try
        {
            var platform = MakeLinuxPlatform(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            var helper = new Ut99KeyBindingHelper();

            await helper.ApplyWasdMovementAsync(MakeGame(), platform, installPath, CancellationToken.None);

            var userIniPath = Path.Combine(systemDir, "User.ini");
            Assert.True(File.Exists(userIniPath));
            Assert.Contains("S=MoveBackward", await File.ReadAllLinesAsync(userIniPath));
        }
        finally
        {
            Directory.Delete(installPath, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyWasdMovementAsync_Throws_WhenNoIniAvailable()
    {
        var installPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(installPath, "System64"));

        try
        {
            var platform = MakeLinuxPlatform(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            var helper = new Ut99KeyBindingHelper();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => helper.ApplyWasdMovementAsync(MakeGame(), platform, installPath, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(installPath, recursive: true);
        }
    }
}
