using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace UTLauncher.Core.Processes;

public sealed class ProcessRunner(ILogger<ProcessRunner> logger)
{
    public Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken) =>
        RunAsync(fileName, arguments, workingDirectory, environment: null, cancellationToken);

    public async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string>? environment,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Running external process: {FileName} {Arguments} (cwd={WorkingDirectory}, env={Environment})",
            fileName,
            string.Join(' ', arguments),
            workingDirectory ?? Environment.CurrentDirectory,
            environment is null ? "(none)" : string.Join(' ', environment.Select(kv => $"{kv.Key}={kv.Value}")));

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (key, value) in environment ?? new Dictionary<string, string>())
        {
            startInfo.Environment[key] = value;
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var result = new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());

        logger.LogInformation("Process {FileName} exited with code {ExitCode}", fileName, result.ExitCode);
        if (!result.Succeeded)
        {
            logger.LogWarning("Process {FileName} stderr: {Stderr}", fileName, result.StandardError);
        }

        return result;
    }

    private void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to kill cancelled process {FileName}", process.StartInfo.FileName);
        }
    }
}
