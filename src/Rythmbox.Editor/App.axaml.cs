using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Rythmbox.Editor.ViewModels;
using Rythmbox.Editor.Views;

namespace Rythmbox.Editor;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new EditorViewModel();
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.ShutdownRequested += (_, _) => viewModel.Dispose();

            if (desktop.Args is { Length: > 0 } args && File.Exists(args[0]))
            {
                viewModel.OpenFile(args[0]);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
