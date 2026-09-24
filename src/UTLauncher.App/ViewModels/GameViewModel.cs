using Avalonia.Media.Imaging;
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
    private readonly FolderPicker? _folderPicker;
    private CancellationTokenSource? _installCancellation;

    public GameViewModel(GameEntry game, AppServices services, FolderPicker? folderPicker)
    {
        _game = game;
        _services = services;
        _folderPicker = folderPicker;
        InstallPath = ComputeDefaultInstallPath();
        IsSupported = SupportedGameIds.Contains(game.Id);
        StatusText = IsSupported ? "Not installed" : "Not available yet";
    }

    // Ut4Installer requires the destination folder to be literally named "UnrealTournament" (the
    // game zip's own top-level folder); UT99/UT2004 accept any name, so this just keeps their
    // folder distinguishable when several games share the same install root.
    private string SubfolderName => Id == "ut4" ? "UnrealTournament" : Id.ToUpperInvariant();

    private string ComputeDefaultInstallPath()
    {
        var defaultRoot = _services.DefaultInstallPathFor(Id.ToUpperInvariant());
        return Id == "ut4" ? Path.Combine(defaultRoot, SubfolderName) : defaultRoot;
    }

    public string Id => _game.Id;

    public string Name => _game.Name;

    public string VersionCode => _game.VersionCode;

    public bool IsSupported { get; }

    public bool HasAccountRegistration => !string.IsNullOrWhiteSpace(_game.AccountRegistrationUrl);

    // We don't bundle the games' own trademarked logos in this repo/app - instead, once a game
    // is installed, its icon is read straight from a logo file already sitting inside the
    // user's own (hash-verified, legitimately obtained) install folder. Until then, or if no
    // such file is known/found (UT4), IconFallbackText is shown in a plain generated badge.
    private static readonly Dictionary<string, string> LogoRelativePathByGameId = new()
    {
        // "Help/Unreal.ico" - despite the shared generic filename, each game's installer ships
        // its own distinct icon there (verified: UT99's is a bronze crest, UT2004's a blue/gold
        // disc), and it's a proper small square icon rather than the wide splash-screen banners
        // also present (UnrealTournamentLogo.bmp / UT2004Logo.bmp).
        ["ut99"] = "Help/Unreal.ico",
        ["ut2004"] = "Help/Unreal.ico",
    };

    [ObservableProperty]
    public partial Bitmap? IconBitmap { get; set; }

    public string IconFallbackText => Id switch
    {
        "ut99" => "99",
        "ut2004" => "'04",
        "ut4" => "UT4",
        _ => "?",
    };

    // Only UT99 needs the WASD key-binding fix; the gear menu's Uninstall entry applies to
    // every installed game.
    public bool HasWasdFix => Id == "ut99";

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyWasdMovementCommand))]
    [NotifyCanExecuteChangedFor(nameof(UninstallCommand))]
    [NotifyPropertyChangedFor(nameof(NeedsUpdate))]
    [NotifyPropertyChangedFor(nameof(ShowInstallButton))]
    [NotifyPropertyChangedFor(nameof(InstallButtonLabel))]
    public partial bool IsInstalled { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [NotifyPropertyChangedFor(nameof(NeedsUpdate))]
    [NotifyPropertyChangedFor(nameof(ShowInstallButton))]
    [NotifyPropertyChangedFor(nameof(InstallButtonLabel))]
    public partial string? InstalledVersionCode { get; set; }

    // True once RefreshStatusAsync (or a completed install) confirms the installed version code
    // no longer matches what the manifest currently ships, so a re-run of the same install flow
    // is offered as "Update" instead of hiding the button entirely.
    public bool NeedsUpdate => IsInstalled && InstalledVersionCode is not null && InstalledVersionCode != VersionCode;

    public bool ShowInstallButton => !IsInstalled || NeedsUpdate;

    public string InstallButtonLabel => NeedsUpdate ? "Update" : "Install";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelInstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    [NotifyCanExecuteChangedFor(nameof(UninstallCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    [NotifyCanExecuteChangedFor(nameof(UninstallCommand))]
    public partial bool IsLaunching { get; set; }

    [ObservableProperty]
    public partial bool IsIndeterminate { get; set; }

    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InstallPath { get; set; }

    [ObservableProperty]
    public partial string? SettingsFeedback { get; set; }

    [ObservableProperty]
    public partial bool IsConfirmingUninstall { get; set; }

    public async Task RefreshStatusAsync()
    {
        var record = await _services.Registry.GetAsync(_game.Id, CancellationToken.None).ConfigureAwait(false);
        Dispatcher.UIThread.Post(() =>
        {
            if (record is null)
            {
                IsInstalled = false;
                InstalledVersionCode = null;
                StatusText = IsSupported ? "Not installed" : "Not available yet";
                return;
            }

            InstallPath = record.InstallPath;
            InstalledVersionCode = record.VersionCode;
            IsInstalled = true;
            StatusText = record.VersionCode == VersionCode
                ? $"Installed ({record.VersionCode})"
                : $"Installed ({record.VersionCode}) - manifest now has {VersionCode}";
            TryLoadIcon();
        });
    }

    private void TryLoadIcon()
    {
        if (!IsInstalled || !LogoRelativePathByGameId.TryGetValue(Id, out var relativeLogoPath))
        {
            IconBitmap = null;
            return;
        }

        var fullPath = Path.Combine(InstallPath, relativeLogoPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            IconBitmap = null;
            return;
        }

        try
        {
            using var stream = File.OpenRead(fullPath);
            IconBitmap = new Bitmap(stream);
        }
        catch (Exception ex)
        {
            _services.LoggerFactory.CreateLogger<GameViewModel>().LogDebug(ex, "Could not load icon for {GameId}", Id);
            IconBitmap = null;
        }
    }

    private bool CanInstall() => IsSupported && !IsBusy && !IsLaunching && (!IsInstalled || NeedsUpdate);

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        // Updating an existing install re-runs the same installer in place against the same
        // InstallPath (Downloader skips files that already match the manifest hash, and
        // extraction overwrites): picking a different folder here would silently leave the old
        // install untouched elsewhere instead of updating it, so only prompt for a fresh install.
        if (!IsInstalled && _folderPicker is not null)
        {
            var chosenRoot = await _folderPicker
                .PickFolderAsync($"Choose where to install {Name}", Path.GetDirectoryName(InstallPath))
                .ConfigureAwait(true);
            if (chosenRoot is null)
            {
                return;
            }

            InstallPath = Path.Combine(chosenRoot, SubfolderName);
        }

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
                InstalledVersionCode = record.VersionCode;
                StatusText = $"Installed ({record.VersionCode})";
                InstallPath = record.InstallPath;
                TryLoadIcon();
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
                _services.UmuRunner,
                _services.Registry,
                _services.Platform,
                _services.LoggerFactory.CreateLogger<Ut4Installer>());
            return installer.InstallAsync(_services.Manifest, _game, InstallPath, progress, cancellationToken);
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
                _services.Registry,
                _services.UmuRunner,
                _services.Platform,
                _services.LoggerFactory.CreateLogger<Core.Games.GameLauncher>());
            await launcher.LaunchAsync(_services.Manifest, _game, InstallPath, CancellationToken.None).ConfigureAwait(false);
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

    private bool CanApplyWasdMovement() => IsInstalled;

    [RelayCommand(CanExecute = nameof(CanApplyWasdMovement))]
    private async Task ApplyWasdMovementAsync()
    {
        try
        {
            var helper = new Ut99KeyBindingHelper();
            await helper.ApplyWasdMovementAsync(_game, _services.Platform, InstallPath, CancellationToken.None)
                .ConfigureAwait(true);
            SettingsFeedback = "Done - W/A/S/D now move/strafe. Restart the game if it's running.";
        }
        catch (Exception ex)
        {
            _services.LoggerFactory.CreateLogger<GameViewModel>().LogWarning(ex, "Failed to apply WASD movement for {GameId}", Id);
            SettingsFeedback = $"Failed: {ex.Message}";
        }

        _ = ClearSettingsFeedbackAfterDelayAsync();
    }

    private async Task ClearSettingsFeedbackAfterDelayAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        SettingsFeedback = null;
    }

    [RelayCommand]
    private void RequestUninstall() => IsConfirmingUninstall = true;

    [RelayCommand]
    private void CancelUninstall() => IsConfirmingUninstall = false;

    private bool CanUninstall() => IsInstalled && !IsBusy && !IsLaunching;

    [RelayCommand(CanExecute = nameof(CanUninstall))]
    private async Task UninstallAsync()
    {
        IsConfirmingUninstall = false;
        IsBusy = true;

        try
        {
            var uninstaller = new GameUninstaller(_services.Registry, _services.LoggerFactory.CreateLogger<GameUninstaller>());
            await uninstaller.UninstallAsync(Id, CancellationToken.None).ConfigureAwait(true);

            IsInstalled = false;
            InstalledVersionCode = null;
            StatusText = IsSupported ? "Not installed" : "Not available yet";
            InstallPath = ComputeDefaultInstallPath();
            IconBitmap = null;
            SettingsFeedback = "Uninstalled.";
        }
        catch (Exception ex)
        {
            _services.LoggerFactory.CreateLogger<GameViewModel>().LogError(ex, "Failed to uninstall {GameId}", Id);
            SettingsFeedback = $"Uninstall failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }

        _ = ClearSettingsFeedbackAfterDelayAsync();
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
