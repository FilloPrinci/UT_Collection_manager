using System.Net;
using UTLauncher.Core.Tests.Download;
using UTLauncher.Core.Updates;

namespace UTLauncher.Core.Tests.Updates;

public class UpdateCheckerTests
{
    private static UpdateChecker CreateChecker(FakeHttpMessageHandler handler) => new(new HttpClient(handler));

    private static FakeHttpMessageHandler HandlerReturningTag(string tagName) => new(_ =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""{"tag_name": "{{tagName}}"}"""),
        });

    [Fact]
    public async Task CheckAsync_ReportsUpdateAvailable_WhenLatestIsNewer()
    {
        var checker = CreateChecker(HandlerReturningTag("v0.2.0"));

        var result = await checker.CheckAsync("v0.1.4", CancellationToken.None);

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("v0.2.0", result.LatestVersion);
    }

    [Fact]
    public async Task CheckAsync_ReportsNoUpdate_WhenLatestIsSame()
    {
        var checker = CreateChecker(HandlerReturningTag("v0.1.4"));

        var result = await checker.CheckAsync("v0.1.4", CancellationToken.None);

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckAsync_ReportsNoUpdate_WhenLatestIsOlder()
    {
        var checker = CreateChecker(HandlerReturningTag("v0.1.0"));

        var result = await checker.CheckAsync("v0.1.4", CancellationToken.None);

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckAsync_ComparesVersionsNumerically_NotLexicographically()
    {
        // A naive string comparison would say "v0.10.0" < "v0.2.0".
        var checker = CreateChecker(HandlerReturningTag("v0.10.0"));

        var result = await checker.CheckAsync("v0.2.0", CancellationToken.None);

        Assert.True(result.IsUpdateAvailable);
    }

    [Theory]
    [InlineData("dev")]
    [InlineData("0.0.0-dev")]
    [InlineData("not-a-version")]
    public async Task CheckAsync_NeverReportsUpdate_ForLocalOrUnparseableVersion(string currentVersion)
    {
        var handler = new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("Should not make a network request for a local/unparseable current version."));
        var checker = CreateChecker(handler);

        var result = await checker.CheckAsync(currentVersion, CancellationToken.None);

        Assert.False(result.IsUpdateAvailable);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CheckAsync_ReturnsNoUpdate_WhenReleaseTagIsUnparseable()
    {
        var checker = CreateChecker(HandlerReturningTag("not-a-version"));

        var result = await checker.CheckAsync("v0.1.4", CancellationToken.None);

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckAsync_IgnoresBuildMetadataSuffix()
    {
        var checker = CreateChecker(HandlerReturningTag("v0.1.4"));

        var result = await checker.CheckAsync("v0.1.4+a1b2c3d", CancellationToken.None);

        Assert.False(result.IsUpdateAvailable);
    }
}
