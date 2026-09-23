using System.Formats.Tar;
using System.Text;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
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

    // SharpCompress's TarWriter cannot write symlink entries, so this fixture is built with
    // the BCL's System.Formats.Tar.TarWriter instead (which supports TarEntryType.SymbolicLink),
    // then bzip2-compressed with SharpCompress's own BZip2Stream — still no external tool needed.
    private static string BuildTarBz2WithSymlink()
    {
        using var tarStream = new MemoryStream();
        using (var tarWriter = new TarWriter(tarStream, TarEntryFormat.Gnu, leaveOpen: true))
        {
            var fileEntry = new GnuTarEntry(TarEntryType.RegularFile, "realbin")
            {
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes("#!/bin/sh\necho hi\n")),
            };
            tarWriter.WriteEntry(fileEntry);

            var linkEntry = new GnuTarEntry(TarEntryType.SymbolicLink, "linkbin") { LinkName = "realbin" };
            tarWriter.WriteEntry(linkEntry);
        }

        tarStream.Position = 0;

        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".tar.bz2");
        using var fileOut = File.Create(path);
        using var bz2 = BZip2Stream.Create(fileOut, CompressionMode.Compress, false, false, false);
        tarStream.CopyTo(bz2);
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

    [Fact]
    public async Task ExtractAsync_RecreatesSymbolicLinksFromTarBz2()
    {
        var archivePath = BuildTarBz2WithSymlink();
        var destination = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var extractor = new ArchiveExtractor();

            await extractor.ExtractAsync(archivePath, destination, progress: null, CancellationToken.None);

            var linkPath = Path.Combine(destination, "linkbin");
            Assert.Equal("realbin", new FileInfo(linkPath).LinkTarget);
            Assert.Equal("#!/bin/sh\necho hi\n", await File.ReadAllTextAsync(linkPath));
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
