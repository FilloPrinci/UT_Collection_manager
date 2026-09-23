namespace UTLauncher.Core.Download;

public sealed record DownloadResult(string Path, long Size, string Sha256Hex, bool ResumedFromPartial);
