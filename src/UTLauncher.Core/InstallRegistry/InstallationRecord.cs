namespace UTLauncher.Core.InstallRegistry;

public sealed record InstallationRecord(
    string GameId,
    string InstallPath,
    string VersionCode,
    string Platform,
    IReadOnlyDictionary<string, string> SourceHashes,
    DateTimeOffset InstalledAtUtc,
    string? PrefixPath = null);
