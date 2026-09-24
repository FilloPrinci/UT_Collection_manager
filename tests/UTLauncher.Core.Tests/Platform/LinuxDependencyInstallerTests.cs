using UTLauncher.Core.Platform;

namespace UTLauncher.Core.Tests.Platform;

public class LinuxDependencyInstallerTests
{
    // Pure mapping, no I/O: safe to test directly without risking a real pkexec/package-manager
    // invocation (which EnsureInstalledAsync itself would attempt on a system missing the library
    // - not something a test should ever trigger for real).
    [Theory]
    [InlineData("fedora", "openal-soft", new[] { "dnf", "install", "-y", "openal-soft" })]
    [InlineData("ubuntu", "libopenal1", new[] { "apt-get", "install", "-y", "libopenal1" })]
    [InlineData("debian", "libsdl3-0", new[] { "apt-get", "install", "-y", "libsdl3-0" })]
    [InlineData("linuxmint", "libomp5", new[] { "apt-get", "install", "-y", "libomp5" })]
    [InlineData("arch", "openal", new[] { "pacman", "-S", "--noconfirm", "openal" })]
    [InlineData("endeavouros", "sdl3", new[] { "pacman", "-S", "--noconfirm", "sdl3" })]
    public void BuildPackageManagerInstallArgs_ReturnsExpectedCommand(string distroId, string packageName, string[] expected)
    {
        var result = LinuxDependencyInstaller.BuildPackageManagerInstallArgs(distroId, packageName);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, "openal")]
    [InlineData("gentoo", "openal")]
    public void BuildPackageManagerInstallArgs_ReturnsNull_ForUnknownDistro(string? distroId, string packageName)
    {
        Assert.Null(LinuxDependencyInstaller.BuildPackageManagerInstallArgs(distroId, packageName));
    }

    [Fact]
    public void BuildPackageManagerInstallArgs_ReturnsNull_WhenPackageNameIsNull()
    {
        Assert.Null(LinuxDependencyInstaller.BuildPackageManagerInstallArgs("ubuntu", null));
    }

    [Fact]
    public async Task EnsureInstalledAsync_IsANoOp_OnWindows()
    {
        // The real Linux path (library lookup via ldconfig, install via pkexec) can't be safely
        // exercised in a test - this only proves the OS guard short-circuits before any of that.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var processRunner = new UTLauncher.Core.Processes.ProcessRunner(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UTLauncher.Core.Processes.ProcessRunner>.Instance);
        var libraryLocator = new SystemLibraryLocator(processRunner);
        var installer = new LinuxDependencyInstaller(
            libraryLocator, processRunner,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LinuxDependencyInstaller>.Instance);

        await installer.EnsureInstalledAsync(["libopenal.so.1"], progress: null, CancellationToken.None);
    }
}
