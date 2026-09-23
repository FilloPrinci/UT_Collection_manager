using UTLauncher.Core.Platform;

namespace UTLauncher.Core.Tests.Platform;

public sealed class FakeEnvironmentInfo : IEnvironmentInfo
{
    private readonly Dictionary<string, string?> _variables = new();
    private readonly Dictionary<Environment.SpecialFolder, string> _folders = new();

    public FakeEnvironmentInfo SetVariable(string name, string? value)
    {
        _variables[name] = value;
        return this;
    }

    public FakeEnvironmentInfo SetFolder(Environment.SpecialFolder folder, string path)
    {
        _folders[folder] = path;
        return this;
    }

    public string? GetEnvironmentVariable(string name) => _variables.GetValueOrDefault(name);

    public string GetFolderPath(Environment.SpecialFolder folder) => _folders[folder];
}
