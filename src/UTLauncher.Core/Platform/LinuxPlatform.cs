namespace UTLauncher.Core.Platform;

public sealed class LinuxPlatform(IEnvironmentInfo environment) : IPlatform
{
    public string Id => "linux-x64";

    public string GetRootDirectory()
    {
        var dataHome = environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(dataHome))
        {
            dataHome = Path.Combine(environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        return Path.Combine(dataHome, "UTLauncher");
    }

    public string GetLogDirectory() => Path.Combine(GetRootDirectory(), "logs");
}
