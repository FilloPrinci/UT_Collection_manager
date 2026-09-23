using UTLauncher.Core.InstallRegistry;

namespace UTLauncher.Core.Tests.InstallRegistry;

public class InstallationRegistryTests
{
    private static InstallationRecord MakeRecord(string gameId, string versionCode = "v1") => new(
        GameId: gameId,
        InstallPath: $"/home/tester/Games/{gameId}",
        VersionCode: versionCode,
        Platform: "linux-x64",
        SourceHashes: new Dictionary<string, string> { ["iso"] = new string('a', 64) },
        InstalledAtUtc: DateTimeOffset.UtcNow);

    private static string TempRegistryPath() =>
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

    [Fact]
    public async Task LoadAsync_ReturnsEmptyList_WhenFileMissing()
    {
        var registry = new InstallationRegistry(TempRegistryPath());

        var records = await registry.LoadAsync(CancellationToken.None);

        Assert.Empty(records);
    }

    [Fact]
    public async Task UpsertAsync_ThenLoadAsync_RoundTripsRecord()
    {
        var path = TempRegistryPath();
        try
        {
            var registry = new InstallationRegistry(path);
            var record = MakeRecord("ut99");

            await registry.UpsertAsync(record, CancellationToken.None);
            var records = await registry.LoadAsync(CancellationToken.None);

            var loaded = Assert.Single(records);
            Assert.Equal(record.GameId, loaded.GameId);
            Assert.Equal(record.InstallPath, loaded.InstallPath);
            Assert.Equal(record.VersionCode, loaded.VersionCode);
            Assert.Equal(record.Platform, loaded.Platform);
            Assert.Equal(record.SourceHashes["iso"], loaded.SourceHashes["iso"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task UpsertAsync_ReplacesExistingRecord_ForSameGameId()
    {
        var path = TempRegistryPath();
        try
        {
            var registry = new InstallationRegistry(path);
            await registry.UpsertAsync(MakeRecord("ut99", "v1"), CancellationToken.None);
            await registry.UpsertAsync(MakeRecord("ut99", "v2"), CancellationToken.None);

            var records = await registry.LoadAsync(CancellationToken.None);

            var loaded = Assert.Single(records);
            Assert.Equal("v2", loaded.VersionCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task UpsertAsync_KeepsRecordsForDifferentGames()
    {
        var path = TempRegistryPath();
        try
        {
            var registry = new InstallationRegistry(path);
            await registry.UpsertAsync(MakeRecord("ut99"), CancellationToken.None);
            await registry.UpsertAsync(MakeRecord("ut2004"), CancellationToken.None);

            var records = await registry.LoadAsync(CancellationToken.None);

            Assert.Equal(2, records.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RemoveAsync_DeletesOnlyMatchingRecord()
    {
        var path = TempRegistryPath();
        try
        {
            var registry = new InstallationRegistry(path);
            await registry.UpsertAsync(MakeRecord("ut99"), CancellationToken.None);
            await registry.UpsertAsync(MakeRecord("ut2004"), CancellationToken.None);

            await registry.RemoveAsync("ut99", CancellationToken.None);
            var records = await registry.LoadAsync(CancellationToken.None);

            var remaining = Assert.Single(records);
            Assert.Equal("ut2004", remaining.GameId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNotFound()
    {
        var registry = new InstallationRegistry(TempRegistryPath());

        var record = await registry.GetAsync("ut4", CancellationToken.None);

        Assert.Null(record);
    }
}
