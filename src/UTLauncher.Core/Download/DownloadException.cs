namespace UTLauncher.Core.Download;

public sealed class DownloadException(string message, Exception? innerException = null)
    : Exception(message, innerException);
