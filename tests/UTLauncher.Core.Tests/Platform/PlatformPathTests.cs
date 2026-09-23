using UTLauncher.Core.Platform;

namespace UTLauncher.Core.Tests.Platform;

public class PlatformPathTests
{
    [Fact]
    public void LinuxPlatform_UsesXdgDataHome_WhenSet()
    {
        var env = new FakeEnvironmentInfo().SetVariable("XDG_DATA_HOME", "/custom/data");
        var platform = new LinuxPlatform(env);

        Assert.Equal("/custom/data/UTLauncher", platform.GetRootDirectory());
        Assert.Equal("/custom/data/UTLauncher/logs", platform.GetLogDirectory());
    }

    [Fact]
    public void LinuxPlatform_FallsBackToLocalShare_WhenXdgDataHomeUnset()
    {
        var env = new FakeEnvironmentInfo()
            .SetVariable("XDG_DATA_HOME", null)
            .SetFolder(Environment.SpecialFolder.UserProfile, "/home/tester");
        var platform = new LinuxPlatform(env);

        Assert.Equal("/home/tester/.local/share/UTLauncher", platform.GetRootDirectory());
    }

    [Fact]
    public void LinuxPlatform_FallsBackToLocalShare_WhenXdgDataHomeBlank()
    {
        var env = new FakeEnvironmentInfo()
            .SetVariable("XDG_DATA_HOME", "   ")
            .SetFolder(Environment.SpecialFolder.UserProfile, "/home/tester");
        var platform = new LinuxPlatform(env);

        Assert.Equal("/home/tester/.local/share/UTLauncher", platform.GetRootDirectory());
    }

    [Fact]
    public void WindowsPlatform_UsesLocalApplicationData()
    {
        var env = new FakeEnvironmentInfo()
            .SetFolder(Environment.SpecialFolder.LocalApplicationData, @"C:\Users\tester\AppData\Local");
        var platform = new WindowsPlatform(env);

        Assert.Equal(Path.Combine(@"C:\Users\tester\AppData\Local", "UTLauncher"), platform.GetRootDirectory());
        Assert.Equal(Path.Combine(@"C:\Users\tester\AppData\Local", "UTLauncher", "logs"), platform.GetLogDirectory());
    }

    [Fact]
    public void PlatformIds_MatchManifestKeys()
    {
        Assert.Equal("linux-x64", new LinuxPlatform(new FakeEnvironmentInfo()).Id);
        Assert.Equal("windows", new WindowsPlatform(new FakeEnvironmentInfo()).Id);
    }
}
