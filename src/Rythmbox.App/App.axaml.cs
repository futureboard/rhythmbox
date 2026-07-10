using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Rythmbox.App.ViewModels;
using Rythmbox.App.Views;

namespace Rythmbox.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                WindowState = string.Equals(Environment.GetEnvironmentVariable("RYTHMBOX_FULLSCREEN"), "1", StringComparison.Ordinal)
                    ? WindowState.FullScreen
                    : WindowState.Normal,
            };
            desktop.MainWindow = mainWindow;
            desktop.ShutdownRequested += (_, _) => mainWindow.DisposeRuntime();
            mainWindow.Opened += async (_, _) => await mainWindow.InitializeRuntimeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
