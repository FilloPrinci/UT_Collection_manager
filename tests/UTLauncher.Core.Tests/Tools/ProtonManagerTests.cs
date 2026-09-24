using System.Formats.Tar;
using System.IO.Compression;
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

public class ProtonManagerTests
{
    private static string Sha256Of(byte[] data) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data));

    private static byte[] MakeProtonTarGz(string topLevelName, byte[] protonScriptContent)
    {
        using var outerStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outerStream, CompressionLevel.Fastest, leaveOpen: true))
        using (var writer = new TarWriter(gzipStream, TarEntryFormat.Pax, leaveOpen: true))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, $"{topLevelName}/proton")
            {
                DataStream = new MemoryStream(protonScriptContent),
            };
            writer.WriteEntry(entry);
        }

        return outerStream.ToArray();
    }

    private static CorePlatform.IPlatform MakePlatform(string rootDir)
    {
        var env = new FakeEnvironmentInfo().SetVariable("XDG_DATA_HOME", rootDir);
        return new CorePlatform.LinuxPlatform(env);
    }

    private static Manifest.Manifest MakeManifest(ToolEntry proton) =>
        new(1, "2026-01-01", null, new ToolsSection(null, null, proton), []);

    [Fact]
    public async Task EnsureAvailableAsync_DownloadsAndExtracts_TheProtonBuild()
    {
        var scriptContent = Encoding.UTF8.GetBytes("#!/bin/sh\necho proton\n");
        var tarGzBytes = MakeProtonTarGz("GE-Proton-test", scriptContent);

        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(tarGzBytes) });
        var downloader = new Downloader(new HttpClient(handler), NullLogger<Downloader>.Instance);

        var rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var platform = MakePlatform(rootDir);
        var protonManager = new ProtonManager(downloader, platform);

        var toolEntry = new ToolEntry(
            Notes: null,
            Windows: null,
            Linux: new ToolPlatformFile("https://example.com/proton.tar.gz", Sha256Of(tarGzBytes), Exe: null, "GE-Proton-test"));
        var manifest = MakeManifest(toolEntry);

        try
        {
            var protonDirectory = await protonManager.EnsureAvailableAsync(manifest, progress: null, CancellationToken.None);

            Assert.EndsWith("GE-Proton-test", protonDirectory);
            var protonScript = Path.Combine(protonDirectory, "proton");
            Assert.True(File.Exists(protonScript));
            Assert.Equal(scriptContent, await File.ReadAllBytesAsync(protonScript));

            if (!OperatingSystem.IsWindows())
            {
                var mode = File.GetUnixFileMode(protonScript);
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
    public async Task EnsureAvailableAsync_DoesNotRedownload_WhenAlreadyExtracted()
    {
        var scriptContent = Encoding.UTF8.GetBytes("#!/bin/sh\necho proton\n");
        var tarGzBytes = MakeProtonTarGz("GE-Proton-test", scriptContent);
        var requestCount = 0;

        var handler = new FakeHttpMessageHandler(_ =>
        {
            requestCount++;
            return requestCount == 1
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(tarGzBytes) }
                : throw new InvalidOperationException("Should not re-download an already-extracted Proton build.");
        });
        var downloader = new Downloader(new HttpClient(handler), NullLogger<Downloader>.Instance);

        var rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var platform = MakePlatform(rootDir);
        var protonManager = new ProtonManager(downloader, platform);

        var toolEntry = new ToolEntry(
            Notes: null,
            Windows: null,
            Linux: new ToolPlatformFile("https://example.com/proton.tar.gz", Sha256Of(tarGzBytes), Exe: null, "GE-Proton-test"));
        var manifest = MakeManifest(toolEntry);

        try
        {
            await protonManager.EnsureAvailableAsync(manifest, progress: null, CancellationToken.None);
            var secondDirectory = await protonManager.EnsureAvailableAsync(manifest, progress: null, CancellationToken.None);

            Assert.Equal(1, requestCount);
            Assert.True(File.Exists(Path.Combine(secondDirectory, "proton")));
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
    public async Task EnsureAvailableAsync_Throws_WhenSectionMissing()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No request should have been made."));
        var downloader = new Downloader(new HttpClient(handler), NullLogger<Downloader>.Instance);

        var rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var platform = MakePlatform(rootDir);
        var protonManager = new ProtonManager(downloader, platform);

        var manifest = new Manifest.Manifest(1, "2026-01-01", null, new ToolsSection(null, null, null), []);

        await Assert.ThrowsAsync<ToolNotConfiguredException>(
            () => protonManager.EnsureAvailableAsync(manifest, progress: null, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureAvailableAsync_Throws_WhenNameIsTodo()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No request should have been made."));
        var downloader = new Downloader(new HttpClient(handler), NullLogger<Downloader>.Instance);

        var rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var platform = MakePlatform(rootDir);
        var protonManager = new ProtonManager(downloader, platform);

        var toolEntry = new ToolEntry(
            Notes: null,
            Windows: null,
            Linux: new ToolPlatformFile("https://example.com/proton.tar.gz", "TODO", Exe: null, "TODO"));
        var manifest = MakeManifest(toolEntry);

        await Assert.ThrowsAsync<ToolNotConfiguredException>(
            () => protonManager.EnsureAvailableAsync(manifest, progress: null, CancellationToken.None));
    }
}
