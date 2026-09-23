using UTLauncher.Core.InstallRegistry;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Platform;

namespace UTLauncher.Core.Games;

/// <summary>
/// Checks that a game the registry says is installed is still actually there: the install path
/// exists, the launch executable is present (and, on Linux, executable), and the installed
/// version still matches what the manifest currently expects.
/// </summary>
public sealed class InstallationVerifier(InstallationRegistry registry, IPlatform platform)
{
    public async Task<VerificationResult> VerifyAsync(GameEntry game, CancellationToken cancellationToken)
    {
        var issues = new List<string>();

        var record = await registry.GetAsync(game.Id, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            issues.Add($"'{game.Id}' is not registered as installed.");
            return new VerificationResult(false, issues);
        }

        if (!Directory.Exists(record.InstallPath))
        {
            issues.Add($"Install path does not exist: {record.InstallPath}");
            return new VerificationResult(false, issues);
        }

        if (!string.Equals(record.Platform, platform.Id, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"Installed for platform '{record.Platform}', but running on '{platform.Id}'.");
        }

        if (!string.Equals(record.VersionCode, game.VersionCode, StringComparison.Ordinal))
        {
            issues.Add(
                $"Installed version ({record.VersionCode}) differs from the manifest's current version ({game.VersionCode}).");
        }

        if (game.Launch is not null &&
            game.Launch.TryGetValue(platform.Id, out var launchEntry) &&
            !string.IsNullOrEmpty(launchEntry.Exe))
        {
            var exePath = Path.Combine(record.InstallPath, launchEntry.Exe.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(exePath))
            {
                issues.Add($"Launch executable not found: {exePath}");
            }
            else if (!OperatingSystem.IsWindows())
            {
                var mode = File.GetUnixFileMode(exePath);
                if (!mode.HasFlag(UnixFileMode.UserExecute))
                {
                    issues.Add($"Launch executable is not marked executable: {exePath}");
                }
            }
        }

        return new VerificationResult(issues.Count == 0, issues);
    }
}
