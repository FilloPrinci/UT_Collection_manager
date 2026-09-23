using Microsoft.Extensions.Logging;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Platform;
using UTLauncher.Core.Processes;

namespace UTLauncher.Core.Games;

/// <summary>
/// Starts an installed game's executable, resolved from the manifest's per-platform "launch" entry.
/// </summary>
public sealed class GameLauncher(ProcessRunner processRunner, IPlatform platform, ILogger<GameLauncher> logger)
{
    public async Task<ProcessResult> LaunchAsync(
        GameEntry game,
        string installPath,
        CancellationToken cancellationToken)
    {
        if (game.Launch is null || !game.Launch.TryGetValue(platform.Id, out var launchEntry) ||
            string.IsNullOrWhiteSpace(launchEntry.Exe))
        {
            throw new InvalidOperationException(
                $"Manifest has no launch entry for '{game.Id}' on platform '{platform.Id}'.");
        }

        var exePath = Path.Combine(installPath, launchEntry.Exe.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException(
                $"Launch executable not found: '{exePath}'. Is '{game.Name}' installed correctly?", exePath);
        }

        var workingDirectory = string.IsNullOrWhiteSpace(launchEntry.WorkingDir)
            ? Path.GetDirectoryName(exePath) ?? installPath
            : Path.Combine(installPath, launchEntry.WorkingDir.Replace('/', Path.DirectorySeparatorChar));
        var arguments = string.IsNullOrWhiteSpace(launchEntry.Args)
            ? []
            : launchEntry.Args.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        logger.LogInformation("Launching {GameName} ({ExePath})", game.Name, exePath);

        var result = await processRunner.RunAsync(exePath, arguments, workingDirectory, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("{GameName} exited with code {ExitCode}", game.Name, result.ExitCode);
        return result;
    }
}
