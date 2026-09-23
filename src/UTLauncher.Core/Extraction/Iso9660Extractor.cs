using DiscUtils.Iso9660;
using UTLauncher.Core.Tasks;

namespace UTLauncher.Core.Extraction;

public sealed class Iso9660Extractor
{
    public async Task ExtractAsync(
        string isoPath,
        string destinationDirectory,
        Func<string, bool>? shouldExtract,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);

        await using var isoStream = File.OpenRead(isoPath);
        using var reader = new CDReader(isoStream, joliet: true);

        // DiscUtils.Iso9660's GetFiles(path, pattern, SearchOption.AllDirectories) returns
        // nothing on real-world discs, and even the single-argument GetFiles(path) overload -
        // despite recursing correctly on ISOs built with CDBuilder in tests - only returns the
        // root-level entries on an actual commercially mastered ISO (verified against the real
        // UT_GOTY_CD1.iso, where it silently dropped 794 of 796 files). Walking the directory
        // tree manually via GetDirectories/GetFiles per level is the only approach that reliably
        // sees every file on both synthetic and real discs.
        var rawEntries = WalkFiles(reader, @"\");
        var entries = rawEntries
            .Select(raw => (Raw: raw, RelativePath: ToRelativePath(raw)))
            .Where(e => shouldExtract is null || shouldExtract(e.RelativePath))
            .ToList();

        var fileName = Path.GetFileName(isoPath);

        for (var i = 0; i < entries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationPath = Path.Combine(destinationDirectory, entries[i].RelativePath);
            var destinationEntryDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationEntryDirectory))
            {
                Directory.CreateDirectory(destinationEntryDirectory);
            }

            using (var source = reader.OpenFile(entries[i].Raw, FileMode.Open))
            await using (var target = File.Create(destinationPath))
            {
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            }

            progress?.Report(TaskProgress.Determinate($"Extracting {fileName}", i + 1, entries.Count));
        }
    }

    private static List<string> WalkFiles(CDReader reader, string directory)
    {
        var files = new List<string>(reader.GetFiles(directory));
        foreach (var subdirectory in reader.GetDirectories(directory))
        {
            files.AddRange(WalkFiles(reader, subdirectory));
        }

        return files;
    }

    private static string ToRelativePath(string isoPath)
    {
        var withoutVersion = isoPath.Split(';')[0];
        var trimmed = withoutVersion.TrimStart('\\', '/');
        return trimmed.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }
}
