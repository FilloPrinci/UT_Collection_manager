namespace UTLauncher.Core.Games;

/// <summary>
/// UT4UU's InstallInfo.bin format (SPEC.md §6.3 step 5): a plain .NET BinaryWriter stream, in
/// order - 6 bools (createShortcut, isDryRun, createSymbolicLinks, upgradeEngineModules,
/// refreshingExperience, tryToInstallInLocalGameServer), 3 strings (sourceLocation,
/// installLocation, replacementSuffix), 2 bytes (platformTarget, buildConfiguration).
/// </summary>
public sealed record InstallInfo(
    bool CreateShortcut,
    bool IsDryRun,
    bool CreateSymbolicLinks,
    bool UpgradeEngineModules,
    bool RefreshingExperience,
    bool TryToInstallInLocalGameServer,
    string SourceLocation,
    string InstallLocation,
    string ReplacementSuffix,
    byte PlatformTarget,
    byte BuildConfiguration);

public static class InstallInfoBinCodec
{
    public static InstallInfo Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return new InstallInfo(
            CreateShortcut: reader.ReadBoolean(),
            IsDryRun: reader.ReadBoolean(),
            CreateSymbolicLinks: reader.ReadBoolean(),
            UpgradeEngineModules: reader.ReadBoolean(),
            RefreshingExperience: reader.ReadBoolean(),
            TryToInstallInLocalGameServer: reader.ReadBoolean(),
            SourceLocation: reader.ReadString(),
            InstallLocation: reader.ReadString(),
            ReplacementSuffix: reader.ReadString(),
            PlatformTarget: reader.ReadByte(),
            BuildConfiguration: reader.ReadByte());
    }

    public static void Write(Stream stream, InstallInfo info)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(info.CreateShortcut);
        writer.Write(info.IsDryRun);
        writer.Write(info.CreateSymbolicLinks);
        writer.Write(info.UpgradeEngineModules);
        writer.Write(info.RefreshingExperience);
        writer.Write(info.TryToInstallInLocalGameServer);
        writer.Write(info.SourceLocation);
        writer.Write(info.InstallLocation);
        writer.Write(info.ReplacementSuffix);
        writer.Write(info.PlatformTarget);
        writer.Write(info.BuildConfiguration);
    }

    /// <summary>
    /// Reads the template InstallInfo.bin the game ships with and rewrites it with only
    /// sourceLocation/installLocation replaced, everything else preserved unchanged (SPEC.md
    /// §6.3 step 5).
    /// </summary>
    public static void RewriteLocations(string path, string sourceLocation, string installLocation)
    {
        InstallInfo original;
        using (var readStream = File.OpenRead(path))
        {
            original = Read(readStream);
        }

        var updated = original with { SourceLocation = sourceLocation, InstallLocation = installLocation };

        using var writeStream = File.Create(path);
        Write(writeStream, updated);
    }
}
