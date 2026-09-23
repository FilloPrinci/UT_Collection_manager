using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UTLauncher.App.Services;
using UTLauncher.Core.Logging;

namespace UTLauncher.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly AppServices? _services;

    public ObservableCollection<GameViewModel> Games { get; } = [];

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public DoctorViewModel? Doctor { get; }

    [ObservableProperty]
    public partial bool IsConsoleVisible { get; set; }

    [ObservableProperty]
    public partial bool IsDoctorVisible { get; set; }

    [ObservableProperty]
    public partial string? StartupError { get; set; }

    /// <summary>Design-time only constructor (see MainWindow.axaml's Design.DataContext).</summary>
    public MainViewModel()
    {
    }

    public MainViewModel(AppServices? services, string? startupError)
    {
        _services = services;
        StartupError = startupError;

        if (services is null)
        {
            return;
        }

        Doctor = new DoctorViewModel(services);

        services.LoggingSession.MemorySink.EntryWritten += OnLogEntryWritten;

        foreach (var game in services.Manifest.Games)
        {
            Games.Add(new GameViewModel(game, services));
        }

        _ = RefreshStatusesAsync();
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
    private void ToggleDoctor()
    {
        IsDoctorVisible = !IsDoctorVisible;
        if (IsDoctorVisible && Doctor is { Results.Count: 0 })
        {
            Doctor.RunCommand.Execute(null);
        }
    }
}
