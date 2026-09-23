using System.Text;
using DiscUtils.Iso9660;
using UTLauncher.Core.Extraction;

namespace UTLauncher.Core.Tests.Extraction;

public class Iso9660ExtractorTests
{
    private static string BuildSampleIso()
    {
        var builder = new CDBuilder { UseJoliet = true };
        builder.AddFile("hello.txt", Encoding.UTF8.GetBytes("hello from iso"));
        builder.AddDirectory("sub");
        builder.AddFile("sub/nested.txt", Encoding.UTF8.GetBytes("nested in iso"));
        builder.AddDirectory("sub/deeper");
        builder.AddFile("sub/deeper/verynested.txt", Encoding.UTF8.GetBytes("very nested"));

        var isoPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".iso");
        builder.Build(isoPath);
        return isoPath;
    }

    [Fact]
    public async Task ExtractAsync_ExtractsAllFilesWithCorrectContent()
    {
        var isoPath = BuildSampleIso();
        var destination = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var extractor = new Iso9660Extractor();

            await extractor.ExtractAsync(isoPath, destination, shouldExtract: null, progress: null, CancellationToken.None);

            Assert.Equal("hello from iso", await File.ReadAllTextAsync(Path.Combine(destination, "hello.txt")));
            Assert.Equal("nested in iso", await File.ReadAllTextAsync(Path.Combine(destination, "sub", "nested.txt")));
            Assert.Equal("very nested", await File.ReadAllTextAsync(Path.Combine(destination, "sub", "deeper", "verynested.txt")));
        }
        finally
        {
            File.Delete(isoPath);
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExtractAsync_AppliesShouldExtractFilter()
    {
        var isoPath = BuildSampleIso();
        var destination = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var extractor = new Iso9660Extractor();

            await extractor.ExtractAsync(
                isoPath,
                destination,
                shouldExtract: relativePath => !relativePath.Contains("deeper"),
                progress: null,
                CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(destination, "hello.txt")));
            Assert.True(File.Exists(Path.Combine(destination, "sub", "nested.txt")));
            Assert.False(File.Exists(Path.Combine(destination, "sub", "deeper", "verynested.txt")));
        }
        finally
        {
            File.Delete(isoPath);
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }
        }
    }
}
