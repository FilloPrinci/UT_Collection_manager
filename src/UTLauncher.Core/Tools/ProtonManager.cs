using System.Formats.Tar;
using System.IO.Compression;
using UTLauncher.Core.Download;
using UTLauncher.Core.Platform;
using UTLauncher.Core.Tasks;

namespace UTLauncher.Core.Tools;

/// <summary>
/// Downloads and extracts the pinned GE-Proton build UT4 uses to run on Linux (SPEC.md §6.3,
/// "Linux only"). Unlike ToolManager's tools, this isn't a single executable: the manifest pins a
/// whole tar.gz build by name/hash, extracted once into its own versioned folder and reused after
/// that (a fresh version bumps the manifest's "name" and simply extracts alongside the old one).
/// </summary>
public sealed class ProtonManager(Downloader downloader, IPlatform platform)
{
    public async Task<string> EnsureAvailableAsync(
        Manifest.Manifest manifest,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        var entry = manifest.Tools?.Proton?.Linux
            ?? throw new ToolNotConfiguredException("Section tools.proton.linux missing from the manifest.");

        if (IsMissingOrTodo(entry.Name) || IsMissingOrTodo(entry.Url) || IsMissingOrTodo(entry.Sha256))
        {
            throw new ToolNotConfiguredException(
                "tools.proton.linux does not have a name/URL/hash set in the manifest yet (TODO entry).");
        }

        var protonRoot = Path.Combine(platform.GetRootDirectory(), "tools", "proton");
        var protonDirectory = Path.Combine(protonRoot, entry.Name!);
        var protonScript = Path.Combine(protonDirectory, "proton");

        if (File.Exists(protonScript))
        {
            return protonDirectory;
        }

        Directory.CreateDirectory(protonRoot);
        var archivePath = Path.Combine(protonRoot, entry.Name + ".tar.gz");
        var request = new DownloadRequest([entry.Url!], archivePath, entry.Sha256!);
        await downloader.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);

        progress?.Report(TaskProgress.Indeterminate("Extracting Proton"));
        await using (var fileStream = File.OpenRead(archivePath))
        await using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
        {
            await TarFile.ExtractToDirectoryAsync(gzipStream, protonRoot, overwriteFiles: true, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Delete(archivePath);

        if (!File.Exists(protonScript))
        {
            throw new ToolNotConfiguredException($"'proton' script not found after extraction at '{protonScript}'.");
        }

        MakeExecutableOnUnix(protonScript);
        return protonDirectory;
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
}
