using System.Text;
using SharpCompress.Common;
using SharpCompress.Writers;
using UTLauncher.Core.Extraction;

namespace UTLauncher.Core.Tests.Extraction;

public class ArchiveExtractorTests
{
    private static string BuildArchive(string extension, ArchiveType archiveType, CompressionType compressionType)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + extension);
        using var fileStream = File.Create(path);
        using var writer = WriterFactory.OpenWriter(fileStream, archiveType, new WriterOptions(compressionType));
        writer.Write("hello.txt", new MemoryStream(Encoding.UTF8.GetBytes("hello from archive")));
        writer.Write("sub/nested.txt", new MemoryStream(Encoding.UTF8.GetBytes("nested in archive")));
        return path;
    }

    [Fact]
    public async Task ExtractAsync_ExtractsSevenZipArchive()
    {
        var archivePath = BuildArchive(".7z", ArchiveType.SevenZip, CompressionType.LZMA);
        var destination = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var extractor = new ArchiveExtractor();

            await extractor.ExtractAsync(archivePath, destination, progress: null, CancellationToken.None);

            Assert.Equal("hello from archive", await File.ReadAllTextAsync(Path.Combine(destination, "hello.txt")));
            Assert.Equal("nested in archive", await File.ReadAllTextAsync(Path.Combine(destination, "sub", "nested.txt")));
        }
        finally
        {
            File.Delete(archivePath);
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExtractAsync_ExtractsTarBz2Archive()
    {
        var archivePath = BuildArchive(".tar.bz2", ArchiveType.Tar, CompressionType.BZip2);
        var destination = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var extractor = new ArchiveExtractor();

            await extractor.ExtractAsync(archivePath, destination, progress: null, CancellationToken.None);

            Assert.Equal("hello from archive", await File.ReadAllTextAsync(Path.Combine(destination, "hello.txt")));
            Assert.Equal("nested in archive", await File.ReadAllTextAsync(Path.Combine(destination, "sub", "nested.txt")));
        }
        finally
        {
            File.Delete(archivePath);
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }
        }
    }
}
