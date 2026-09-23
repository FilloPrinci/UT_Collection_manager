using UTLauncher.Core.Manifest;
using ManifestModel = UTLauncher.Core.Manifest.Manifest;

namespace UTLauncher.Core.Tests.ManifestTests;

public class ManifestValidatorTests
{
    private static readonly string ValidHash = new('a', 64);

    private static GameEntry MakeGame(string id, IReadOnlyDictionary<string, SourceFile>? sources = null) =>
        new(
            Id: id,
            Name: $"Game {id}",
            VersionCode: "v1",
            Reference: null,
            Sources: sources ?? new Dictionary<string, SourceFile>(),
            Patch: null,
            Launch: null,
            Network: null,
            DiskSpaceRequiredBytes: null,
            MasterServer: null,
            Ut4uuInstallInfo: null,
            AccountRegistrationUrl: null,
            Dependencies: null,
            CdKey: null);

    private static ManifestModel MakeManifest(params GameEntry[] games) =>
        new(ManifestVersion: 1, Updated: "2026-01-01", Notes: null, Tools: null, Games: games);

    [Fact]
    public void Validate_ReturnsNoErrors_ForWellFormedManifest()
    {
        var source = new SourceFile("f.iso", 100, ValidHash, ["https://example.com/f.iso"], null, null, null);
        var manifest = MakeManifest(MakeGame("ut99", new Dictionary<string, SourceFile> { ["iso"] = source }));

        var result = ManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_ReportsError_ForMalformedHash()
    {
        var source = new SourceFile("f.iso", 100, "not-a-hash", ["https://example.com/f.iso"], null, null, null);
        var manifest = MakeManifest(MakeGame("ut99", new Dictionary<string, SourceFile> { ["iso"] = source }));

        var result = ManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("invalid sha256"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("TODO")]
    [InlineData("todo")]
    public void Validate_ReportsWarning_NotError_ForTodoHash(string? sha256)
    {
        var source = new SourceFile("f.iso", 100, sha256, ["https://example.com/f.iso"], null, null, null);
        var manifest = MakeManifest(MakeGame("ut99", new Dictionary<string, SourceFile> { ["iso"] = source }));

        var result = ManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("TODO"));
    }

    [Fact]
    public void Validate_ReportsError_ForDuplicateGameIds()
    {
        var manifest = MakeManifest(MakeGame("ut99"), MakeGame("ut99"));

        var result = ManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate"));
    }

    [Fact]
    public void Validate_ReportsError_WhenNoGames()
    {
        var manifest = MakeManifest();

        var result = ManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_DoesNotFlagParentHash_WhenAlternativesPresent()
    {
        var alternative = new SourceFile(null, 100, ValidHash, ["https://example.com/f.iso"], null, null, null);
        var source = new SourceFile("f.iso", null, null, null, [alternative], null, null);
        var manifest = MakeManifest(MakeGame("ut2004", new Dictionary<string, SourceFile> { ["iso"] = source }));

        var result = ManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("sources.iso:"));
    }

    [Fact]
    public void Validate_ValidatesEachAlternative()
    {
        var badAlternative = new SourceFile(null, 100, "bad-hash", ["https://example.com/f.iso"], null, null, null);
        var source = new SourceFile("f.iso", null, null, null, [badAlternative], null, null);
        var manifest = MakeManifest(MakeGame("ut2004", new Dictionary<string, SourceFile> { ["iso"] = source }));

        var result = ManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("alternatives[0]"));
    }

    [Fact]
    public void Validate_ReportsError_ForToolExtraFileWithMalformedHash()
    {
        var extraFile = new ToolExtraFile("zlib.dll", "https://example.com/zlib.dll", "not-a-hash", null);
        var windows = new ToolPlatformFile("https://example.com/unshield.exe", ValidHash, "unshield.exe", null, [extraFile]);
        var manifest = new ManifestModel(
            ManifestVersion: 1,
            Updated: "2026-01-01",
            Notes: null,
            Tools: new ToolsSection(new ToolEntry(null, windows, null), null, null),
            Games: [MakeGame("ut99")]);

        var result = ManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("extraFiles[zlib.dll]") && e.Contains("invalid sha256"));
    }

    [Fact]
    public void Validate_ReportsWarning_NotError_ForToolExtraFileWithTodoHash()
    {
        var extraFile = new ToolExtraFile("zlib.dll", "https://example.com/zlib.dll", "TODO", null);
        var windows = new ToolPlatformFile("https://example.com/unshield.exe", ValidHash, "unshield.exe", null, [extraFile]);
        var manifest = new ManifestModel(
            ManifestVersion: 1,
            Updated: "2026-01-01",
            Notes: null,
            Tools: new ToolsSection(new ToolEntry(null, windows, null), null, null),
            Games: [MakeGame("ut99")]);

        var result = ManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("extraFiles[zlib.dll]"));
    }
}
