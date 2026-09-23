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

        foreach (var extraFile in platformFile.ExtraFiles ?? [])
        {
            await EnsureExtraFileAvailableAsync(kind, toolDirectory, extraFile, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        return destinationPath;
    }

    private async Task EnsureExtraFileAvailableAsync(
        ExternalToolKind kind,
        string toolDirectory,
        ToolExtraFile extraFile,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (IsMissingOrTodo(extraFile.Url) || IsMissingOrTodo(extraFile.Sha256) || IsMissingOrTodo(extraFile.FileName))
        {
            throw new ToolNotConfiguredException(
                $"Tool '{kind}' has an extra file without a URL/hash/fileName set in the manifest yet (TODO entry).");
        }

        var destinationPath = Path.Combine(toolDirectory, extraFile.FileName!);
        var request = new DownloadRequest([extraFile.Url!], destinationPath, extraFile.Sha256!, extraFile.Size);
        await downloader.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);
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
