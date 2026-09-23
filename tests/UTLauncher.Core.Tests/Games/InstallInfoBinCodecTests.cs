using UTLauncher.Core.Games;

namespace UTLauncher.Core.Tests.Games;

public class InstallInfoBinCodecTests
{
    private static readonly InstallInfo Sample = new(
        CreateShortcut: true,
        IsDryRun: false,
        CreateSymbolicLinks: true,
        UpgradeEngineModules: false,
        RefreshingExperience: true,
        TryToInstallInLocalGameServer: false,
        SourceLocation: @"C:\Some\Source",
        InstallLocation: @"C:\Some\Install",
        ReplacementSuffix: "_BackUp",
        PlatformTarget: 1,
        BuildConfiguration: 3);

    [Fact]
    public void Write_ThenRead_RoundTripsEveryField()
    {
        using var stream = new MemoryStream();

        InstallInfoBinCodec.Write(stream, Sample);
        stream.Position = 0;
        var result = InstallInfoBinCodec.Read(stream);

        Assert.Equal(Sample, result);
    }

    [Fact]
    public void RewriteLocations_ChangesOnlySourceAndInstallLocation()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            using (var stream = File.Create(path))
            {
                InstallInfoBinCodec.Write(stream, Sample);
            }

            InstallInfoBinCodec.RewriteLocations(path, @"C:\Generic\Install\Path", @"C:\Games\UnrealTournament");

            InstallInfo updated;
            using (var stream = File.OpenRead(path))
            {
                updated = InstallInfoBinCodec.Read(stream);
            }

            Assert.Equal(@"C:\Generic\Install\Path", updated.SourceLocation);
            Assert.Equal(@"C:\Games\UnrealTournament", updated.InstallLocation);

            // Everything else preserved unchanged.
            Assert.Equal(Sample.CreateShortcut, updated.CreateShortcut);
            Assert.Equal(Sample.IsDryRun, updated.IsDryRun);
            Assert.Equal(Sample.CreateSymbolicLinks, updated.CreateSymbolicLinks);
            Assert.Equal(Sample.UpgradeEngineModules, updated.UpgradeEngineModules);
            Assert.Equal(Sample.RefreshingExperience, updated.RefreshingExperience);
            Assert.Equal(Sample.TryToInstallInLocalGameServer, updated.TryToInstallInLocalGameServer);
            Assert.Equal(Sample.ReplacementSuffix, updated.ReplacementSuffix);
            Assert.Equal(Sample.PlatformTarget, updated.PlatformTarget);
            Assert.Equal(Sample.BuildConfiguration, updated.BuildConfiguration);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Write_ProducesThePlainDotNetBinaryWriterLayout()
    {
        // 6 bools (1 byte each) + 3 length-prefixed UTF8 strings + 2 bytes, in that exact order -
        // this is what UT4UU itself reads, so the byte layout is part of the contract, not an
        // implementation detail.
        using var stream = new MemoryStream();

        InstallInfoBinCodec.Write(stream, Sample);
        var bytes = stream.ToArray();

        Assert.Equal(1, bytes[0]); // CreateShortcut = true
        Assert.Equal(0, bytes[1]); // IsDryRun = false
        Assert.Equal(1, bytes[2]); // CreateSymbolicLinks = true
        Assert.Equal(0, bytes[3]); // UpgradeEngineModules = false
        Assert.Equal(1, bytes[4]); // RefreshingExperience = true
        Assert.Equal(0, bytes[5]); // TryToInstallInLocalGameServer = false

        // First string: 7-bit encoded length prefix (fits in one byte for a short string) then
        // its UTF8 bytes.
        var sourceLocationBytes = System.Text.Encoding.UTF8.GetBytes(Sample.SourceLocation);
        Assert.Equal((byte)sourceLocationBytes.Length, bytes[6]);
        Assert.Equal(sourceLocationBytes, bytes.Skip(7).Take(sourceLocationBytes.Length).ToArray());

        Assert.Equal(Sample.PlatformTarget, bytes[^2]);
        Assert.Equal(Sample.BuildConfiguration, bytes[^1]);
    }
}
