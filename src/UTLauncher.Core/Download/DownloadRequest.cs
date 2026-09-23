namespace UTLauncher.Core.Download;

public sealed record DownloadRequest(
    IReadOnlyList<string> Urls,
    string DestinationPath,
    string ExpectedSha256,
    long? ExpectedSize = null);
