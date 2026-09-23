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

        // GetFiles(path) with no pattern/SearchOption is the only overload that reliably
        // recurses the whole Joliet tree in DiscUtils.Iso9660; the pattern-based overloads
        // return nothing for this reader. Entries come back as e.g. "\sub/nested.txt;1"
        // (leading backslash, internal forward slashes, trailing ISO9660 version suffix).
        var rawEntries = reader.GetFiles(@"\");
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

    private static string ToRelativePath(string isoPath)
    {
        var withoutVersion = isoPath.Split(';')[0];
        var trimmed = withoutVersion.TrimStart('\\', '/');
        return trimmed.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }
}
