using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Rythmbox.SampleCreator.ViewModels;
using Rythmbox.SampleCreator.Views;

namespace Rythmbox.SampleCreator;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new SampleCreatorViewModel();
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.ShutdownRequested += (_, _) => viewModel.Dispose();

            if (desktop.Args is { Length: > 0 } args && File.Exists(args[0]))
            {
                viewModel.OpenPreset(args[0]);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
