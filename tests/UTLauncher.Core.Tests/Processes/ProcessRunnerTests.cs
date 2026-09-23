using Microsoft.Extensions.Logging.Abstractions;
using UTLauncher.Core.Processes;

namespace UTLauncher.Core.Tests.Processes;

public class ProcessRunnerTests
{
    private static ProcessRunner CreateRunner() => new(NullLogger<ProcessRunner>.Instance);

    [Fact]
    public async Task RunAsync_CapturesExitCodeAndStdout()
    {
        var runner = CreateRunner();

        var result = await runner.RunAsync(
            "/bin/sh",
            ["-c", "echo hello-stdout; exit 0"],
            workingDirectory: null,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello-stdout", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_CapturesNonZeroExitCodeAndStderr()
    {
        var runner = CreateRunner();

        var result = await runner.RunAsync(
            "/bin/sh",
            ["-c", "echo hello-stderr 1>&2; exit 3"],
            workingDirectory: null,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(3, result.ExitCode);
        Assert.Contains("hello-stderr", result.StandardError);
    }

    [Fact]
    public async Task RunAsync_UsesWorkingDirectory()
    {
        var runner = CreateRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = await runner.RunAsync(
                "/bin/sh",
                ["-c", "pwd"],
                workingDirectory: tempDir,
                CancellationToken.None);

            Assert.Equal(Path.GetFullPath(tempDir), result.StandardOutput.Trim());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        var runner = CreateRunner();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync("/bin/sh", ["-c", "sleep 30"], workingDirectory: null, cts.Token));
    }
}
