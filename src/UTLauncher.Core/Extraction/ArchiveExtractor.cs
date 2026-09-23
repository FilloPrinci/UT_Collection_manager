using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using UTLauncher.Core.Tasks;

namespace UTLauncher.Core.Extraction;

public sealed class ArchiveExtractor
{
    private static readonly string[] StreamedTarExtensions = [".tar.bz2", ".tar.gz", ".tbz2", ".tgz"];

    public async Task ExtractAsync(
        string archivePath,
        string destinationDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);

        if (IsStreamedTarArchive(archivePath))
        {
            await ExtractWithReaderAsync(archivePath, destinationDirectory, progress, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await ExtractWithArchiveAsync(archivePath, destinationDirectory, progress, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static bool IsStreamedTarArchive(string path) =>
        StreamedTarExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    // Tar archives (the Linux patches) can contain symlinks (e.g. System64/ut-bin -> ut-bin-amd64).
    // SharpCompress only recreates them on disk if a SymbolicLinkHandler is supplied; without one
    // the entry is silently skipped and the link never appears on disk.
    private static void CreateSymbolicLink(string destinationPath, string linkTarget)
    {
        if (File.Exists(destinationPath) || new FileInfo(destinationPath).LinkTarget is not null)
        {
            File.Delete(destinationPath);
        }

        File.CreateSymbolicLink(destinationPath, linkTarget);
    }

    private static async Task ExtractWithArchiveAsync(
        string archivePath,
        string destinationDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(archivePath);
        var extractionOptions = new ExtractionOptions
        {
            Overwrite = true,
            ExtractFullPath = true,
            SymbolicLinkHandler = CreateSymbolicLink,
        };

        await using var stream = File.OpenRead(archivePath);
        using var archive = ArchiveFactory.OpenArchive(stream, new ReaderOptions());
        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();

        for (var i = 0; i < entries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = entries[i];
            var destinationPath = Path.Combine(destinationDirectory, entry.Key!.Replace('/', Path.DirectorySeparatorChar));
            var destinationEntryDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationEntryDirectory))
            {
                Directory.CreateDirectory(destinationEntryDirectory);
            }

            await entry.WriteToFileAsync(destinationPath, extractionOptions, cancellationToken).ConfigureAwait(false);
            progress?.Report(TaskProgress.Determinate($"Extracting {fileName}", i + 1, entries.Count));
        }
    }

    private static async Task ExtractWithReaderAsync(
        string archivePath,
        string destinationDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(archivePath);
        var extractionOptions = new ExtractionOptions
        {
            Overwrite = true,
            ExtractFullPath = true,
            SymbolicLinkHandler = CreateSymbolicLink,
        };

        await using var stream = File.OpenRead(archivePath);
        await using var reader = await ReaderFactory.OpenAsyncReader(stream, new ReaderOptions(), cancellationToken)
            .ConfigureAwait(false);

        var extractedCount = 0;
        while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reader.Entry.IsDirectory)
            {
                continue;
            }

            await reader.WriteEntryToDirectoryAsync(destinationDirectory, extractionOptions, cancellationToken)
                .ConfigureAwait(false);
            extractedCount++;
            progress?.Report(TaskProgress.Indeterminate($"Extracting {fileName} ({extractedCount} files so far)"));
        }
    }
}
