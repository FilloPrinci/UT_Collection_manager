namespace UTLauncher.Core.Manifest;

public sealed class ManifestLoadException : Exception
{
    public ManifestLoadException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
