using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using UTLauncher.Core.Download;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Tests.Download;
using UTLauncher.Core.Tests.Platform;
using UTLauncher.Core.Tools;
using CorePlatform = UTLauncher.Core.Platform;

namespace UTLauncher.Core.Tests.Tools;

public class ToolManagerTests
{
    private static string Sha256Of(byte[] data) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data));

    private static Manifest.Manifest MakeManifest(ToolEntry unshield) =>
        new(1, "2026-01-01", null, new ToolsSection(unshield, null, null), []);

    private CorePlatform.IPlatform MakePlatform(string rootDir)
    {
        var env = new FakeEnvironmentInfo().SetVariable("XDG_DATA_HOME", rootDir);
        return new CorePlatform.LinuxPlatform(env);
    }

    [Fact]
    public async Task EnsureAvailableAsync_DownloadsAndMakesExecutable_WhenConfigured()
    {
        var content = Encoding.UTF8.GetBytes("fake unshield binary");
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
        var downloader = new Downloader(new HttpClient(handler), NullLogger<Downloader>.Instance);

        var rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var platform = MakePlatform(rootDir);
        var toolManager = new ToolManager(downloader, platform);

        var toolEntry = new ToolEntry(
            Notes: null,
            Windows: null,
            Linux: new ToolPlatformFile("https://example.com/unshield", Sha256Of(content), "unshield", null));
        var manifest = MakeManifest(toolEntry);

        try
        {
            var path = await toolManager.EnsureAvailableAsync(
                ExternalToolKind.Unshield, manifest, progress: null, CancellationToken.None);

            Assert.True(File.Exists(path));
            Assert.Equal(content, await File.ReadAllBytesAsync(path));
            Assert.Contains("tools", path);
            Assert.Contains("unshield", path.ToLowerInvariant());

            if (!OperatingSystem.IsWindows())
            {
                var mode = File.GetUnixFileMode(path);
                Assert.True(mode.HasFlag(UnixFileMode.UserExecute));
            }
        }
        finally
        {
            if (Directory.Exists(rootDir))
            {
                Directory.Delete(rootDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnsureAvailableAsync_DownloadsExtraFiles_AlongsideTheExecutable()
    {
        var exeContent = Encoding.UTF8.GetBytes("fake unshield.exe");
        var dllContent = Encoding.UTF8.GetBytes("fake zlib.dll");
        var handler = new FakeHttpMessageHandler(request =>
        {
            var content = request.RequestUri!.AbsoluteUri.EndsWith("zlib.dll") ? dllContent : exeContent;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };
        });
        var downloader = new Downloader(new HttpClient(handler), NullLogger<Downloader>.Instance);

        var rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var platform = MakePlatform(rootDir);
        var toolManager = new ToolManager(downloader, platform);

        var extraFile = new ToolExtraFile("zlib.dll", "https://example.com/zlib.dll", Sha256Of(dllContent), dllContent.Length);
        var toolEntry = new ToolEntry(
            Notes: null,
            Windows: null,
            Linux: new ToolPlatformFile("https://example.com/unshield.exe", Sha256Of(exeContent), "unshield.exe", null, [extraFile]));
        var manifest = MakeManifest(toolEntry);

        try
        {
            var path = await toolManager.EnsureAvailableAsync(
                ExternalToolKind.Unshield, manifest, progress: null, CancellationToken.None);

            var dllPath = Path.Combine(Path.GetDirectoryName(path)!, "zlib.dll");
            Assert.True(File.Exists(dllPath));
            Assert.Equal(dllContent, await File.ReadAllBytesAsync(dllPath));
        }
        finally
        {
            if (Directory.Exists(rootDir))
            {
                Directory.Delete(rootDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnsureAvailableAsync_Throws_WhenExtraFileHashIsTodo()
    {
        var exeContent = Encoding.UTF8.GetBytes("fake unshield.exe");
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(exeContent) });
        var downloader = new Downloader(new HttpClient(handler), NullLogger<Downloader>.Instance);

        var rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var platform = MakePlatform(rootDir);
        var toolManager = new ToolManager(downloader, platform);

        var extraFile = new ToolExtraFile("zlib.dll", "TODO", "TODO", null);
        var toolEntry = new ToolEntry(
            Notes: null,
            Windows: null,
            Linux: new ToolPlatformFile("https://example.com/unshield.exe", Sha256Of(exeContent), "unshield.exe", null, [extraFile]));
        var manifest = MakeManifest(toolEntry);

        try
        {
            await Assert.ThrowsAsync<ToolNotConfiguredException>(
                () => toolManager.EnsureAvailableAsync(ExternalToolKind.Unshield, manifest, progress: null, CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(rootDir))
            {
                Directory.Delete(rootDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnsureAvailableAsync_Throws_WhenHashIsTodo()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No request should have been made."));
        var downloader = new Downloader(new HttpClient(handler), NullLogger<Downloader>.Instance);

        var rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var platform = MakePlatform(rootDir);
        var toolManager = new ToolManager(downloader, platform);

        var toolEntry = new ToolEntry(
            Notes: null,
            Windows: null,
            Linux: new ToolPlatformFile("TODO", "TODO", "unshield", null));
        var manifest = MakeManifest(toolEntry);

        await Assert.ThrowsAsync<ToolNotConfiguredException>(
            () => toolManager.EnsureAvailableAsync(ExternalToolKind.Unshield, manifest, progress: null, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureAvailableAsync_Throws_WhenToolSectionMissing()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No request should have been made."));
        var downloader = new Downloader(new HttpClient(handler), NullLogger<Downloader>.Instance);

        var rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var platform = MakePlatform(rootDir);
        var toolManager = new ToolManager(downloader, platform);

        var manifest = new Manifest.Manifest(1, "2026-01-01", null, new ToolsSection(null, null, null), []);

        await Assert.ThrowsAsync<ToolNotConfiguredException>(
            () => toolManager.EnsureAvailableAsync(ExternalToolKind.Unshield, manifest, progress: null, CancellationToken.None));
    }
}
