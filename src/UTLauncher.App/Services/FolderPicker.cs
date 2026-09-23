using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;

namespace UTLauncher.App.Services;

/// <summary>
/// Wraps Avalonia's cross-platform storage provider (native dialog on Windows, XDG desktop
/// portal on Linux) so ViewModels can ask for a folder without depending on a Window/TopLevel.
/// </summary>
public sealed class FolderPicker(TopLevel topLevel, ILogger<FolderPicker> logger)
{
    public async Task<string?> PickFolderAsync(string title, string? suggestedStartPath)
    {
        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        };

        if (!string.IsNullOrWhiteSpace(suggestedStartPath))
        {
            try
            {
                options.SuggestedStartLocation =
                    await topLevel.StorageProvider.TryGetFolderFromPathAsync(suggestedStartPath).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // Best-effort only (e.g. the suggested path doesn't exist yet): fall back to
                // whatever default location the picker itself uses.
                logger.LogWarning(ex, "Could not resolve suggested folder picker location {Path}", suggestedStartPath);
            }
        }

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options).ConfigureAwait(true);
        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }
}
