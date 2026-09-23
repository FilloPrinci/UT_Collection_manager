using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using UTLauncher.App.Services;
using UTLauncher.Core.Games;
using UTLauncher.Core.Manifest;
using UTLauncher.Core.Tasks;

namespace UTLauncher.App.ViewModels;

public partial class GameViewModel : ViewModelBase
{
    private static readonly HashSet<string> SupportedGameIds = ["ut99", "ut2004", "ut4"];

    private readonly GameEntry _game;
    private readonly AppServices _services;
    private CancellationTokenSource? _installCancellation;

    public GameViewModel(GameEntry game, AppServices services)
    {
        _game = game;
        _services = services;
        var defaultPath = services.DefaultInstallPathFor(game.Id.ToUpperInvariant());
        // Ut4Installer requires the destination folder to be literally named "UnrealTournament"
        // (the game zip's own top-level folder), unlike UT99/UT2004's free-form install path.
        InstallPath = game.Id == "ut4" ? Path.Combine(defaultPath, "UnrealTournament") : defaultPath;
        IsSupported = SupportedGameIds.Contains(game.Id);
        StatusText = IsSupported ? "Not installed" : "Not available yet";
    }

    public string Id => _game.Id;

    public string Name => _game.Name;

    public string VersionCode => _game.VersionCode;

    public bool IsSupported { get; }

    public bool HasAccountRegistration => !string.IsNullOrWhiteSpace(_game.AccountRegistrationUrl);

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    public partial bool IsInstalled { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelInstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    public partial bool IsLaunching { get; set; }

    [ObservableProperty]
    public partial bool IsIndeterminate { get; set; }

    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InstallPath { get; set; }

    public async Task RefreshStatusAsync()
    {
        var record = await _services.Registry.GetAsync(_game.Id, CancellationToken.None).ConfigureAwait(false);
        Dispatcher.UIThread.Post(() =>
        {
            if (record is null)
            {
                IsInstalled = false;
                StatusText = IsSupported ? "Not installed" : "Not available yet";
                return;
            }

            InstallPath = record.InstallPath;
            IsInstalled = true;
            StatusText = record.VersionCode == VersionCode
                ? $"Installed ({record.VersionCode})"
                : $"Installed ({record.VersionCode}) - manifest now has {VersionCode}";
        });
    }

    private bool CanInstall() => IsSupported && !IsBusy && !IsLaunching;

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        IsBusy = true;
        IsIndeterminate = true;
        ProgressText = "Starting...";

        _installCancellation = new CancellationTokenSource();
        var progress = new UiTaskProgress(this);

        try
        {
            var record = await RunInstallerAsync(progress, _installCancellation.Token).ConfigureAwait(false);
            Dispatcher.UIThread.Post(() =>
            {
                IsInstalled = true;
                StatusText = $"Installed ({record.VersionCode})";
                InstallPath = record.InstallPath;
            });
        }
        catch (OperationCanceledException)
        {
            Dispatcher.UIThread.Post(() => StatusText = "Cancelled");
        }
        catch (Exception ex)
        {
            _services.LoggerFactory.CreateLogger<GameViewModel>().LogError(ex, "Installation of {GameId} failed", Id);
            Dispatcher.UIThread.Post(() => StatusText = $"Failed: {ex.Message}");
        }
        finally
        {
            _installCancellation.Dispose();
            _installCancellation = null;
            Dispatcher.UIThread.Post(() =>
            {
                IsBusy = false;
                IsIndeterminate = false;
            });
        }
    }

    private Task<Core.InstallRegistry.InstallationRecord> RunInstallerAsync(
        UiTaskProgress progress, CancellationToken cancellationToken)
    {
        if (Id == "ut4")
        {
            var installer = new Ut4Installer(
                _services.Downloader,
                _services.WindowsDependencyInstaller,
                _services.Registry,
                _services.Platform,
                _services.LoggerFactory.CreateLogger<Ut4Installer>());
            return installer.InstallAsync(_game, InstallPath, progress, cancellationToken);
        }

        if (Id == "ut2004")
        {
            var installer = new Ut2004Installer(
                _services.Downloader,
                _services.IsoExtractor,
                _services.ArchiveExtractor,
                _services.ProcessRunner,
                _services.ToolManager,
                _services.SystemLibraryLocator,
                _services.WindowsDependencyInstaller,
                _services.Registry,
                _services.Platform,
                _services.LoggerFactory.CreateLogger<Ut2004Installer>());
            return installer.InstallAsync(_services.Manifest, _game, InstallPath, progress, cancellationToken);
        }

        var ut99Installer = new Ut99Installer(
            _services.Downloader,
            _services.IsoExtractor,
            _services.ArchiveExtractor,
            _services.ProcessRunner,
            _services.WindowsDependencyInstaller,
            _services.Registry,
            _services.Platform,
            _services.LoggerFactory.CreateLogger<Ut99Installer>());
        return ut99Installer.InstallAsync(_game, InstallPath, progress, cancellationToken);
    }

    private bool CanCancelInstall() => IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancelInstall))]
    private void CancelInstall() => _installCancellation?.Cancel();

    private bool CanLaunch() => IsInstalled && !IsBusy && !IsLaunching;

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task LaunchAsync()
    {
        var previousStatus = StatusText;
        IsLaunching = true;
        StatusText = "Running...";

        try
        {
            var launcher = new Core.Games.GameLauncher(
                _services.ProcessRunner,
                _services.Platform,
                _services.LoggerFactory.CreateLogger<Core.Games.GameLauncher>());
            await launcher.LaunchAsync(_game, InstallPath, CancellationToken.None).ConfigureAwait(false);
            Dispatcher.UIThread.Post(() => StatusText = previousStatus);
        }
        catch (Exception ex)
        {
            _services.LoggerFactory.CreateLogger<GameViewModel>().LogError(ex, "Failed to launch {GameId}", Id);
            Dispatcher.UIThread.Post(() => StatusText = $"Launch failed: {ex.Message}");
        }
        finally
        {
            Dispatcher.UIThread.Post(() => IsLaunching = false);
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (!Directory.Exists(InstallPath))
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = InstallPath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _services.LoggerFactory.CreateLogger<GameViewModel>().LogWarning(ex, "Could not open folder {Path}", InstallPath);
        }
    }

    [RelayCommand]
    private void OpenRegistration()
    {
        if (_game.AccountRegistrationUrl is not { } url)
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _services.LoggerFactory.CreateLogger<GameViewModel>().LogWarning(ex, "Could not open registration page {Url}", url);
        }
    }

    private sealed class UiTaskProgress(GameViewModel owner) : IProgress<TaskProgress>
    {
        public void Report(TaskProgress value) => Dispatcher.UIThread.Post(() =>
        {
            owner.IsIndeterminate = value.IsIndeterminate;
            owner.ProgressPercent = value.PercentComplete ?? owner.ProgressPercent;
            owner.ProgressText = value.StepText;
        });
    }
}
