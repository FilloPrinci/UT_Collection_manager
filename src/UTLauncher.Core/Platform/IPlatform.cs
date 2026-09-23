namespace UTLauncher.Core.Platform;

public interface IPlatform
{
    string Id { get; }

    string GetRootDirectory();

    string GetLogDirectory();
}
