using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Rythmbox.Editor.Services;
using Rythmbox.Editor.ViewModels;
using Rythmbox.Editor.Views;

namespace Rythmbox.Editor;

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
            desktop.MainWindow = new MainWindow();
            var fileDialog = new StorageFileDialogService(() => desktop.MainWindow);
            var viewModel = new EditorViewModel(fileDialog);
            desktop.MainWindow.DataContext = viewModel;
            desktop.ShutdownRequested += (_, _) => viewModel.Dispose();

            if (desktop.Args is { Length: > 0 } args && File.Exists(args[0]))
            {
                viewModel.OpenFile(args[0]);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
