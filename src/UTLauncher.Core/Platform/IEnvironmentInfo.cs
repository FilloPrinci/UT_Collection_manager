namespace UTLauncher.Core.Platform;

public interface IEnvironmentInfo
{
    string? GetEnvironmentVariable(string name);

    string GetFolderPath(Environment.SpecialFolder folder);
}
