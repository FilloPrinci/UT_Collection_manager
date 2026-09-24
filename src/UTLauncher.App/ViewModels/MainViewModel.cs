using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using UTLauncher.App.Services;
using UTLauncher.Core.Logging;
using UTLauncher.Core.Updates;

namespace UTLauncher.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly AppServices? _services;
    private readonly ClipboardService? _clipboardService;

    public string AppVersion { get; } = GetAppVersion();

    public ObservableCollection<GameViewModel> Games { get; } = [];

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public DoctorViewModel? Doctor { get; }

    [ObservableProperty]
    public partial bool IsConsoleVisible { get; set; }

    [ObservableProperty]
    public partial bool IsDoctorVisible { get; set; }

    [ObservableProperty]
    public partial string? StartupError { get; set; }

    [ObservableProperty]
    public partial string? CopyLogsFeedback { get; set; }

    [ObservableProperty]
    public partial string? AvailableUpdateVersion { get; set; }

    /// <summary>Design-time only constructor (see MainWindow.axaml's Design.DataContext).</summary>
    public MainViewModel()
    {
    }

    public MainViewModel(AppServices? services, string? startupError, FolderPicker? folderPicker, ClipboardService? clipboardService)
    {
        _services = services;
        _clipboardService = clipboardService;
        StartupError = startupError;

        if (services is null)
        {
            return;
        }

        Doctor = new DoctorViewModel(services);

        services.LoggingSession.MemorySink.EntryWritten += OnLogEntryWritten;

        foreach (var game in services.Manifest.Games)
        {
            Games.Add(new GameViewModel(game, services, folderPicker));
        }

        _ = RefreshStatusesAsync();
        _ = CheckForUpdateAsync();
    }

    private async Task CheckForUpdateAsync()
    {
        if (_services is null)
        {
            return;
        }

        try
        {
            var result = await _services.UpdateChecker.CheckAsync(AppVersion, CancellationToken.None).ConfigureAwait(false);
            if (result.IsUpdateAvailable)
            {
                Dispatcher.UIThread.Post(() => AvailableUpdateVersion = result.LatestVersion);
            }
        }
        catch (Exception ex)
        {
            // Non-critical background check (e.g. offline, GitHub unreachable): log and move on,
            // never surface this as a user-facing error.
            _services.LoggerFactory.CreateLogger<MainViewModel>().LogDebug(ex, "Update check failed");
        }
    }

    [RelayCommand]
    private void OpenReleasesPage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = UpdateChecker.ReleasesPageUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _services?.LoggerFactory.CreateLogger<MainViewModel>().LogWarning(ex, "Could not open the releases page");
        }
    }

    private void OnLogEntryWritten(object? sender, LogEntry entry) => Dispatcher.UIThread.Post(() =>
    {
        LogEntries.Add(entry);
        while (LogEntries.Count > 2000)
        {
            LogEntries.RemoveAt(0);
        }
    });

    private async Task RefreshStatusesAsync()
    {
        foreach (var game in Games)
        {
            await game.RefreshStatusAsync().ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private void ToggleConsole() => IsConsoleVisible = !IsConsoleVisible;

    [RelayCommand]
    private async Task CopyLogsAsync()
    {
        if (_clipboardService is null)
        {
            return;
        }

        var text = string.Join('\n', LogEntries.Select(FormatLogEntry));
        await _clipboardService.SetTextAsync(text).ConfigureAwait(true);

        CopyLogsFeedback = $"Copied {LogEntries.Count} line(s) to the clipboard";
        _ = ClearCopyLogsFeedbackAfterDelayAsync();
    }

    private async Task ClearCopyLogsFeedbackAfterDelayAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(true);
        CopyLogsFeedback = null;
    }

    private static string FormatLogEntry(LogEntry entry)
    {
        var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{entry.Level}] {entry.Message}";
        return entry.Exception is null ? line : $"{line}\n{entry.Exception}";
    }

    [RelayCommand]
    private void ToggleDoctor()
    {
        IsDoctorVisible = !IsDoctorVisible;
        if (IsDoctorVisible && Doctor is { Results.Count: 0 })
        {
            Doctor.RunCommand.Execute(null);
        }
    }

    // <Version> is set from the pushed git tag by the release workflow; local/dev builds fall
    // back to the csproj default ("0.0.0-dev"). Trims off the "+<git-sha>" a deterministic build
    // can append to InformationalVersion, so the UI shows a plain "v0.1.4".
    private static string GetAppVersion()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(informational))
        {
            return "dev";
        }

        var plusIndex = informational.IndexOf('+');
        var version = plusIndex >= 0 ? informational[..plusIndex] : informational;
        return $"v{version}";
    }
}
