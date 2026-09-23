using UTLauncher.Core.Platform;

namespace UTLauncher.Core.Tests.Platform;

public class SystemCheckerTests
{
    [Theory]
    [InlineData("fedora", "libopenal.so.1", "sudo dnf install openal-soft")]
    [InlineData("ubuntu", "libSDL3.so.0", "sudo apt install libsdl3-0")]
    [InlineData("debian", "libomp.so.5", "sudo apt install libomp5")]
    [InlineData("arch", "libopenal.so.1", "sudo pacman -S openal")]
    [InlineData("linuxmint", "libSDL3.so.0", "sudo apt install libsdl3-0")]
    public void InstallHintFor_ReturnsExpectedCommand(string distroId, string libraryName, string expected)
    {
        Assert.Equal(expected, SystemChecker.InstallHintFor(distroId, libraryName));
    }

    [Theory]
    [InlineData(null, "libopenal.so.1")]
    [InlineData("gentoo", "libopenal.so.1")]
    [InlineData("fedora", "libSomethingElse.so.1")]
    public void InstallHintFor_ReturnsNull_ForUnknownDistroOrLibrary(string? distroId, string libraryName)
    {
        Assert.Null(SystemChecker.InstallHintFor(distroId, libraryName));
    }
}
