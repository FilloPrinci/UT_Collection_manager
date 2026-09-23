using System.Text;
using UTLauncher.Core.Manifest;

namespace UTLauncher.Core.Tests.ManifestTests;

public class ManifestLoaderTests
{
    [Fact]
    public async Task LoadBundledAsync_ParsesRealManifest()
    {
        var loader = new ManifestLoader();

        var manifest = await loader.LoadBundledAsync(CancellationToken.None);

        Assert.Equal(1, manifest.ManifestVersion);
        Assert.Equal(3, manifest.Games.Count);
        Assert.Contains(manifest.Games, g => g.Id == "ut99");
        Assert.Contains(manifest.Games, g => g.Id == "ut2004");
        Assert.Contains(manifest.Games, g => g.Id == "ut4");
    }

    [Fact]
    public async Task LoadBundledAsync_ParsedManifest_IsValid()
    {
        var loader = new ManifestLoader();
        var manifest = await loader.LoadBundledAsync(CancellationToken.None);

        var result = ManifestValidator.Validate(manifest);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task LoadFromStreamAsync_ThrowsManifestLoadException_OnMalformedJson()
    {
        var loader = new ManifestLoader();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{ not json"));

        await Assert.ThrowsAsync<ManifestLoadException>(
            () => loader.LoadFromStreamAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task LoadFromFileAsync_ThrowsManifestLoadException_WhenFileMissing()
    {
        var loader = new ManifestLoader();

        await Assert.ThrowsAsync<ManifestLoadException>(
            () => loader.LoadFromFileAsync("/nonexistent/path/manifest.json", CancellationToken.None));
    }

    [Fact]
    public async Task LoadFromStreamAsync_ParsesUt2004AlternativesSources()
    {
        const string json = """
            {
              "manifestVersion": 1,
              "games": [
                {
                  "id": "ut2004",
                  "name": "Unreal Tournament 2004",
                  "versionCode": "UT04-3374p23",
                  "sources": {
                    "iso": {
                      "fileName": "UT2004.iso",
                      "alternatives": [
                        { "size": 100, "sha256": "aa11aa11aa11aa11aa11aa11aa11aa11aa11aa11aa11aa11aa11aa11aa11aa1", "urls": [ "https://example.com/a" ] }
                      ]
                    }
                  }
                }
              ]
            }
            """;
        var loader = new ManifestLoader();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var manifest = await loader.LoadFromStreamAsync(stream, CancellationToken.None);

        var iso = manifest.Games[0].Sources["iso"];
        Assert.Null(iso.Sha256);
        Assert.NotNull(iso.Alternatives);
        Assert.Single(iso.Alternatives);
        Assert.Equal(100, iso.Alternatives[0].Size);
    }
}
