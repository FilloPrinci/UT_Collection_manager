namespace UTLauncher.Core.Platform;

public sealed class WindowsPlatform(IEnvironmentInfo environment) : IPlatform
{
    public string Id => "windows";

    public string GetRootDirectory() =>
        Path.Combine(environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UTLauncher");

    public string GetLogDirectory() => Path.Combine(GetRootDirectory(), "logs");
}
