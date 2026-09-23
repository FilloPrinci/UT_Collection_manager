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
            ?? throw new ToolNotConfiguredException($"Strumento '{kind}' non configurato per la piattaforma '{platform.Id}'.");

        if (IsMissingOrTodo(platformFile.Url) || IsMissingOrTodo(platformFile.Sha256))
        {
            throw new ToolNotConfiguredException(
                $"Strumento '{kind}' non ha ancora URL/hash impostati nel manifest (voce TODO).");
        }

        if (string.IsNullOrWhiteSpace(platformFile.Exe))
        {
            throw new ToolNotConfiguredException($"Nome eseguibile mancante per lo strumento '{kind}'.");
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
        ExternalToolKind.SevenZip => manifest.Tools?.SevenZip
            ?? throw new ToolNotConfiguredException("Sezione tools.sevenZip mancante nel manifest."),
        ExternalToolKind.Unshield => manifest.Tools?.Unshield
            ?? throw new ToolNotConfiguredException("Sezione tools.unshield mancante nel manifest."),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static ToolPlatformFile? GetPlatformFile(ToolEntry entry, string platformId) => platformId switch
    {
        "windows" => entry.Windows,
        "linux-x64" => entry.Linux,
        _ => null,
    };
}
