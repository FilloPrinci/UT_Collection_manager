using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace UTLauncher.App.Services;

/// <summary>Wraps Avalonia's clipboard access so ViewModels don't need a Window/TopLevel reference.</summary>
public sealed class ClipboardService(TopLevel topLevel)
{
    public Task SetTextAsync(string text) => topLevel.Clipboard?.SetTextAsync(text) ?? Task.CompletedTask;
}
