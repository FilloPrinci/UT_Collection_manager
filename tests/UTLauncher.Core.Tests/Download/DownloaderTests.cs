using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using UTLauncher.Core.Download;

namespace UTLauncher.Core.Tests.Download;

public class DownloaderTests
{
    private static string Sha256Of(byte[] data) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data));

    private static Downloader CreateDownloader(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<Downloader>.Instance);

    [Fact]
    public async Task DownloadAsync_SavesFile_AndVerifiesHash_OnFreshDownload()
    {
        var content = Encoding.UTF8.GetBytes("contenuto di prova");
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content),
        });
        var downloader = CreateDownloader(handler);
        var destination = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var request = new DownloadRequest(["https://example.com/file.bin"], destination, Sha256Of(content));

            var result = await downloader.DownloadAsync(request, progress: null, CancellationToken.None);

            Assert.Equal(content.Length, result.Size);
            Assert.False(result.ResumedFromPartial);
            Assert.True(File.Exists(destination));
            Assert.False(File.Exists(destination + ".part"));
            Assert.Equal(content, await File.ReadAllBytesAsync(destination));
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Fact]
    public async Task DownloadAsync_TriesNextMirror_WhenFirstFails()
    {
        var content = Encoding.UTF8.GetBytes("contenuto dal secondo mirror");
        var attemptedUrls = new List<string>();
        var handler = new FakeHttpMessageHandler(req =>
        {
            attemptedUrls.Add(req.RequestUri!.ToString());
            if (req.RequestUri!.ToString().Contains("mirror1"))
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };
        });
        var downloader = CreateDownloader(handler);
        var destination = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var request = new DownloadRequest(
                ["https://mirror1.example.com/file.bin", "https://mirror2.example.com/file.bin"],
                destination,
                Sha256Of(content));

            var result = await downloader.DownloadAsync(request, progress: null, CancellationToken.None);

            Assert.Equal(2, attemptedUrls.Count);
            Assert.Equal(content.Length, result.Size);
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Fact]
    public async Task DownloadAsync_ResumesFromPartialFile_UsingRangeHeader()
    {
        var firstHalf = Encoding.UTF8.GetBytes("prima metà ");
        var secondHalf = Encoding.UTF8.GetBytes("seconda metà");
        var fullContent = firstHalf.Concat(secondHalf).ToArray();

        var handler = new FakeHttpMessageHandler(req =>
        {
            Assert.NotNull(req.Headers.Range);
            Assert.Equal(firstHalf.Length, (long)req.Headers.Range!.Ranges.First().From!);

            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent(secondHalf),
            };
            return response;
        });
        var downloader = CreateDownloader(handler);
        var destination = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var partPath = destination + ".part";

        try
        {
            await File.WriteAllBytesAsync(partPath, firstHalf);

            var request = new DownloadRequest(["https://example.com/file.bin"], destination, Sha256Of(fullContent));
            var result = await downloader.DownloadAsync(request, progress: null, CancellationToken.None);

            Assert.True(result.ResumedFromPartial);
            Assert.Equal(fullContent, await File.ReadAllBytesAsync(destination));
        }
        finally
        {
            File.Delete(destination);
            File.Delete(partPath);
        }
    }

    [Fact]
    public async Task DownloadAsync_RestartsFromScratch_WhenServerIgnoresRange()
    {
        var staleData = Encoding.UTF8.GetBytes("dati vecchi e incompleti");
        var freshContent = Encoding.UTF8.GetBytes("contenuto fresco completo");

        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(freshContent) });
        var downloader = CreateDownloader(handler);
        var destination = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var partPath = destination + ".part";

        try
        {
            await File.WriteAllBytesAsync(partPath, staleData);

            var request = new DownloadRequest(["https://example.com/file.bin"], destination, Sha256Of(freshContent));
            var result = await downloader.DownloadAsync(request, progress: null, CancellationToken.None);

            Assert.False(result.ResumedFromPartial);
            Assert.Equal(freshContent, await File.ReadAllBytesAsync(destination));
        }
        finally
        {
            File.Delete(destination);
            File.Delete(partPath);
        }
    }

    [Fact]
    public async Task DownloadAsync_ThrowsDownloadException_AndCleansUpPart_WhenHashMismatchOnAllMirrors()
    {
        var content = Encoding.UTF8.GetBytes("contenuto sbagliato");
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
        var downloader = CreateDownloader(handler);
        var destination = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var wrongExpectedHash = new string('a', 64);

        try
        {
            var request = new DownloadRequest(["https://example.com/file.bin"], destination, wrongExpectedHash);

            var ex = await Assert.ThrowsAsync<DownloadException>(
                () => downloader.DownloadAsync(request, progress: null, CancellationToken.None));

            Assert.IsType<HashMismatchException>(ex.InnerException);
            Assert.False(File.Exists(destination));
            Assert.False(File.Exists(destination + ".part"));
        }
        finally
        {
            File.Delete(destination);
            File.Delete(destination + ".part");
        }
    }

    [Fact]
    public async Task DownloadAsync_SkipsNetwork_WhenValidFileAlreadyExists()
    {
        var content = Encoding.UTF8.GetBytes("già scaricato in precedenza");
        var handler = new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("Non doveva essere chiamata alcuna richiesta HTTP."));
        var downloader = CreateDownloader(handler);
        var destination = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            await File.WriteAllBytesAsync(destination, content);
            var request = new DownloadRequest(["https://example.com/file.bin"], destination, Sha256Of(content));

            var result = await downloader.DownloadAsync(request, progress: null, CancellationToken.None);

            Assert.Empty(handler.Requests);
            Assert.Equal(content.Length, result.Size);
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Fact]
    public async Task DownloadAsync_RedownloadsFile_WhenExistingFileHashDoesNotMatch()
    {
        var staleContent = Encoding.UTF8.GetBytes("contenuto vecchio non valido");
        var freshContent = Encoding.UTF8.GetBytes("contenuto nuovo valido");
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(freshContent) });
        var downloader = CreateDownloader(handler);
        var destination = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            await File.WriteAllBytesAsync(destination, staleContent);
            var request = new DownloadRequest(["https://example.com/file.bin"], destination, Sha256Of(freshContent));

            var result = await downloader.DownloadAsync(request, progress: null, CancellationToken.None);

            Assert.Single(handler.Requests);
            Assert.Equal(freshContent, await File.ReadAllBytesAsync(destination));
        }
        finally
        {
            File.Delete(destination);
        }
    }
}
