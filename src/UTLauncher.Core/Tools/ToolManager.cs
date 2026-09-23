using UTLauncher.Core.Download;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Platform;
using UTLauncher.Core.Tasks;

namespace UTLauncher.Core.Tools;

public sealed class ToolManager(Downloader downloader, IPlatform platform)
{
    public async Task<string> EnsureAvailableAsync(
        ExternalToolKind kind,
        Manifest.Manifest manifest,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        var toolEntry = GetToolEntry(kind, manifest);
        var platformFile = GetPlatformFile(toolEntry, platform.Id)
            ?? throw new ToolNotConfiguredException($"Tool '{kind}' not configured for platform '{platform.Id}'.");

        if (IsMissingOrTodo(platformFile.Url) || IsMissingOrTodo(platformFile.Sha256))
        {
            throw new ToolNotConfiguredException(
                $"Tool '{kind}' does not have a URL/hash set in the manifest yet (TODO entry).");
        }

        if (string.IsNullOrWhiteSpace(platformFile.Exe))
        {
            throw new ToolNotConfiguredException($"Missing executable name for tool '{kind}'.");
        }

        var toolDirectory = Path.Combine(platform.GetRootDirectory(), "tools", kind.ToString().ToLowerInvariant());
        var destinationPath = Path.Combine(toolDirectory, platformFile.Exe);

        var request = new DownloadRequest([platformFile.Url!], destinationPath, platformFile.Sha256!);
        await downloader.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);

        MakeExecutableOnUnix(destinationPath);

        return destinationPath;
    }

    private static bool IsMissingOrTodo(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals("TODO", StringComparison.OrdinalIgnoreCase);

    private static void MakeExecutableOnUnix(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var mode = File.GetUnixFileMode(path);
        File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }

    private static ToolEntry GetToolEntry(ExternalToolKind kind, Manifest.Manifest manifest) => kind switch
    {
        ExternalToolKind.Unshield => manifest.Tools?.Unshield
            ?? throw new ToolNotConfiguredException("Section tools.unshield missing from the manifest."),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static ToolPlatformFile? GetPlatformFile(ToolEntry entry, string platformId) => platformId switch
    {
        "windows" => entry.Windows,
        "linux-x64" => entry.Linux,
        _ => null,
    };
}
