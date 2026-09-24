using Microsoft.Extensions.Logging;
using UTLauncher.Core.InstallRegistry;

namespace UTLauncher.Core.Games;

/// <summary>
/// Removes an installed game: deletes its install folder (which already contains the
/// "Installer" subfolder with any cached downloads, so a single recursive delete covers both),
/// its Wine/Proton prefix if it has one (UT4 on Linux), and its registry entry.
/// </summary>
public sealed class GameUninstaller(InstallationRegistry registry, ILogger<GameUninstaller> logger)
{
    public async Task UninstallAsync(string gameId, CancellationToken cancellationToken)
    {
        var record = await registry.GetAsync(gameId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            throw new InvalidOperationException($"'{gameId}' is not registered as installed.");
        }

        logger.LogInformation("Uninstalling {GameId} from {InstallPath}", gameId, record.InstallPath);

        if (Directory.Exists(record.InstallPath))
        {
            Directory.Delete(record.InstallPath, recursive: true);
        }

        if (!string.IsNullOrEmpty(record.PrefixPath) && Directory.Exists(record.PrefixPath))
        {
            logger.LogInformation("Removing prefix {PrefixPath}", record.PrefixPath);
            Directory.Delete(record.PrefixPath, recursive: true);
        }

        await registry.RemoveAsync(gameId, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Uninstalled {GameId}", gameId);
    }
}
