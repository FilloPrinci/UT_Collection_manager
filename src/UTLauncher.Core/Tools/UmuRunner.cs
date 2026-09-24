using UTLauncher.Core.Manifest;
using UTLauncher.Core.Processes;
using UTLauncher.Core.Tasks;

namespace UTLauncher.Core.Tools;

/// <summary>
/// Runs a command through umu-run (SPEC.md §6.3, "Linux only"): resolves umu-run and the pinned
/// Proton build (downloading/extracting either on first use), then invokes umu-run against a
/// given Wine prefix. Shared by UT4's installer (winetricks verbs) and its launcher (the game
/// itself), since both need the exact same WINEPREFIX/PROTONPATH/GAMEID setup.
/// </summary>
public sealed class UmuRunner(ToolManager toolManager, ProtonManager protonManager, ProcessRunner processRunner)
{
    // No umu-database entry exists for this unofficial/community build of UT4, so there is no
    // game-specific fix to look up - "umu-default" is umu-run's own documented default for this case.
    private const string GameId = "umu-default";

    public async Task<ProcessResult> RunAsync(
        Manifest.Manifest manifest,
        string prefixPath,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        IProgress<TaskProgress>? progress,
        CancellationToken cancellationToken)
    {
        var umuPath = await toolManager.EnsureAvailableAsync(ExternalToolKind.Umu, manifest, progress, cancellationToken)
            .ConfigureAwait(false);
        var protonPath = await protonManager.EnsureAvailableAsync(manifest, progress, cancellationToken)
            .ConfigureAwait(false);

        var environment = new Dictionary<string, string>
        {
            ["WINEPREFIX"] = prefixPath,
            ["PROTONPATH"] = protonPath,
            ["GAMEID"] = GameId,
        };

        return await processRunner.RunAsync(umuPath, arguments, workingDirectory, environment, cancellationToken)
            .ConfigureAwait(false);
    }
}
