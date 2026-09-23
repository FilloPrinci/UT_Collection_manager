using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UTLauncher.App.Services;
using UTLauncher.Core.Platform;

namespace UTLauncher.App.ViewModels;

public partial class DoctorViewModel(AppServices services) : ViewModelBase
{
    public ObservableCollection<SystemCheckResult> Results { get; } = [];

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [RelayCommand]
    private async Task RunAsync()
    {
        IsRunning = true;
        try
        {
            var checker = new SystemChecker(services.SystemLibraryLocator);
            var results = await checker.RunAsync(CancellationToken.None).ConfigureAwait(false);

            Dispatcher.UIThread.Post(() =>
            {
                Results.Clear();
                foreach (var result in results)
                {
                    Results.Add(result);
                }
            });
        }
        finally
        {
            Dispatcher.UIThread.Post(() => IsRunning = false);
        }
    }
}
