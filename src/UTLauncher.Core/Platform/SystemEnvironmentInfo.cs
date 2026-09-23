namespace UTLauncher.Core.Platform;

public sealed class SystemEnvironmentInfo : IEnvironmentInfo
{
    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    public string GetFolderPath(Environment.SpecialFolder folder) => Environment.GetFolderPath(folder);
}
