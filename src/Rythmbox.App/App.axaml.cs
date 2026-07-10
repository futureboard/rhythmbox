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
            // Classic desktop (Windows / X11 / Wayland windowed): host the view in a window.
            var mainWindow = new MainWindow
            {
                WindowState = string.Equals(Environment.GetEnvironmentVariable("RYTHMBOX_FULLSCREEN"), "1", StringComparison.Ordinal)
                    ? WindowState.FullScreen
                    : WindowState.Normal,
            };
            desktop.MainWindow = mainWindow;
            desktop.ShutdownRequested += (_, _) => mainWindow.DisposeRuntime();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            // Single-view lifetime, e.g. DRM/KMS on embedded Linux: no window, the
            // MainView is rendered directly onto the framebuffer surface.
            singleView.MainView = new MainView();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
