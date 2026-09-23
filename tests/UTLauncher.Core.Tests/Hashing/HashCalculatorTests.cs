using System.Text;
using UTLauncher.Core.Hashing;

namespace UTLauncher.Core.Tests.Hashing;

public class HashCalculatorTests
{
    [Fact]
    public async Task ComputeAsync_ReturnsKnownSha256_ForEmptyInput()
    {
        using var stream = new MemoryStream();

        var result = await HashCalculator.ComputeAsync(stream, progress: null, CancellationToken.None);

        Assert.Equal(0, result.Size);
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", result.Sha256Hex);
    }

    [Fact]
    public async Task ComputeAsync_ReturnsKnownSha256_ForAbcInput()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("abc"));

        var result = await HashCalculator.ComputeAsync(stream, progress: null, CancellationToken.None);

        Assert.Equal(3, result.Size);
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", result.Sha256Hex);
    }

    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        public List<T> Reported { get; } = [];

        public void Report(T value) => Reported.Add(value);
    }

    [Fact]
    public async Task ComputeAsync_ReportsProgress_AsBytesAreRead()
    {
        var data = new byte[500_000];
        Random.Shared.NextBytes(data);
        using var stream = new MemoryStream(data);
        var progress = new SynchronousProgress<long>();

        var result = await HashCalculator.ComputeAsync(stream, progress, CancellationToken.None);

        Assert.Equal(data.Length, result.Size);
        Assert.NotEmpty(progress.Reported);
        Assert.Equal(data.Length, progress.Reported[^1]);
    }

    [Fact]
    public async Task ComputeFileAsync_MatchesStreamComputation()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "abc");

            var result = await HashCalculator.ComputeFileAsync(path, progress: null, CancellationToken.None);

            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", result.Sha256Hex);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("ABCDEF", "abcdef", true)]
    [InlineData("abcdef", "abcdef", true)]
    [InlineData("abcdef", "123456", false)]
    public void Matches_IsCaseInsensitive(string a, string b, bool expected)
    {
        Assert.Equal(expected, HashCalculator.Matches(a, b));
    }
}
