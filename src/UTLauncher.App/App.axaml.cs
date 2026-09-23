using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using UTLauncher.App.Services;
using UTLauncher.App.ViewModels;
using UTLauncher.App.Views;

namespace UTLauncher.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppServices? services = null;
            string? startupError = null;

            try
            {
                services = AppServices.CreateAsync(verbose: false, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                startupError = $"Could not start: {ex.Message}";
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(services, startupError),
            };

            if (services is not null)
            {
                desktop.Exit += (_, _) => services.Dispose();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
