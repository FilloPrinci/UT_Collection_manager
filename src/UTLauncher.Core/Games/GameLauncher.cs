using Microsoft.Extensions.Logging;
using UTLauncher.Core.InstallRegistry;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Platform;
using UTLauncher.Core.Processes;
using UTLauncher.Core.Tools;

namespace UTLauncher.Core.Games;

/// <summary>
/// Starts an installed game's executable, resolved from the manifest's per-platform "launch"
/// entry. A "runner": "umu" entry (UT4 on Linux, SPEC.md §6.3) means the exe is a Windows binary
/// that has to run through umu-run/Proton in its install-time prefix rather than directly.
/// </summary>
public sealed class GameLauncher(
    ProcessRunner processRunner,
    InstallationRegistry registry,
    UmuRunner umuRunner,
    IPlatform platform,
    ILogger<GameLauncher> logger)
{
    public async Task<ProcessResult> LaunchAsync(
        Manifest.Manifest manifest,
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

        ProcessResult result;
        if (string.Equals(launchEntry.Runner, "umu", StringComparison.OrdinalIgnoreCase))
        {
            var record = await registry.GetAsync(game.Id, cancellationToken).ConfigureAwait(false);
            if (record?.PrefixPath is not { } prefixPath)
            {
                throw new InvalidOperationException(
                    $"'{game.Name}' has no Proton prefix recorded; try reinstalling it.");
            }

            logger.LogInformation("Launching {GameName} via umu-run ({ExePath}, prefix={PrefixPath})", game.Name, exePath, prefixPath);
            result = await umuRunner.RunAsync(
                manifest, prefixPath, [exePath, .. arguments], workingDirectory, progress: null, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            logger.LogInformation("Launching {GameName} ({ExePath})", game.Name, exePath);
            result = await processRunner.RunAsync(exePath, arguments, workingDirectory, cancellationToken)
                .ConfigureAwait(false);
        }

        logger.LogInformation("{GameName} exited with code {ExitCode}", game.Name, result.ExitCode);
        return result;
    }
}
