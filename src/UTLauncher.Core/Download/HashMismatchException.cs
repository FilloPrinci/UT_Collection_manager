namespace UTLauncher.Core.Download;

public sealed class HashMismatchException(string message, string expectedSha256, string actualSha256)
    : Exception(message)
{
    public string ExpectedSha256 { get; } = expectedSha256;

    public string ActualSha256 { get; } = actualSha256;
}
