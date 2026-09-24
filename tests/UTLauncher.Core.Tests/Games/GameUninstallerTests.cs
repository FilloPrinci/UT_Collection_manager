using Microsoft.Extensions.Logging.Abstractions;
using UTLauncher.Core.Games;
using UTLauncher.Core.InstallRegistry;

namespace UTLauncher.Core.Tests.Games;

public class GameUninstallerTests
{
    private static string TempRegistryPath() => Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

    [Fact]
    public async Task UninstallAsync_DeletesInstallFolder_AndRemovesRegistryEntry()
    {
        var installPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(installPath, "System64"));
        await File.WriteAllTextAsync(Path.Combine(installPath, "System64", "ut-bin"), "fake binary");

        var registryPath = TempRegistryPath();
        var registry = new InstallationRegistry(registryPath);
        await registry.UpsertAsync(
            new InstallationRecord("ut99", installPath, "UT99-469e", "linux-x64", new Dictionary<string, string>(), DateTimeOffset.UtcNow),
            CancellationToken.None);

        var uninstaller = new GameUninstaller(registry, NullLogger<GameUninstaller>.Instance);

        await uninstaller.UninstallAsync("ut99", CancellationToken.None);

        Assert.False(Directory.Exists(installPath));
        Assert.Null(await registry.GetAsync("ut99", CancellationToken.None));
    }

    [Fact]
    public async Task UninstallAsync_AlsoDeletesPrefixPath_WhenSet()
    {
        var installPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var prefixPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(installPath);
        Directory.CreateDirectory(prefixPath);

        var registry = new InstallationRegistry(TempRegistryPath());
        await registry.UpsertAsync(
            new InstallationRecord("ut4", installPath, "UT4-1.1.0", "linux-x64", new Dictionary<string, string>(), DateTimeOffset.UtcNow, PrefixPath: prefixPath),
            CancellationToken.None);

        var uninstaller = new GameUninstaller(registry, NullLogger<GameUninstaller>.Instance);

        await uninstaller.UninstallAsync("ut4", CancellationToken.None);

        Assert.False(Directory.Exists(installPath));
        Assert.False(Directory.Exists(prefixPath));
    }

    [Fact]
    public async Task UninstallAsync_Throws_WhenNotRegistered()
    {
        var registry = new InstallationRegistry(TempRegistryPath());
        var uninstaller = new GameUninstaller(registry, NullLogger<GameUninstaller>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => uninstaller.UninstallAsync("ut99", CancellationToken.None));
    }

    [Fact]
    public async Task UninstallAsync_DoesNotThrow_WhenInstallFolderAlreadyMissing()
    {
        var installPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var registry = new InstallationRegistry(TempRegistryPath());
        await registry.UpsertAsync(
            new InstallationRecord("ut99", installPath, "UT99-469e", "linux-x64", new Dictionary<string, string>(), DateTimeOffset.UtcNow),
            CancellationToken.None);

        var uninstaller = new GameUninstaller(registry, NullLogger<GameUninstaller>.Instance);

        await uninstaller.UninstallAsync("ut99", CancellationToken.None);

        Assert.Null(await registry.GetAsync("ut99", CancellationToken.None));
    }
}
