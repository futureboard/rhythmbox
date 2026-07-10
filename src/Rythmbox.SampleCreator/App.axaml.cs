using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Rythmbox.Core.Engine;
using Rythmbox.Editor.Services;
using Rythmbox.SampleCreator.ViewModels;
using Rythmbox.SampleCreator.Views;

namespace Rythmbox.SampleCreator;

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

            var engine = new PlaybackEngine();
            engine.Start();
            var kitPlayer = new KitSamplePlayer(engine);
            var kitSession = new KitSession(kitPlayer);
            kitSession.TryLoadDefaultPreset();

            var viewModel = new SampleCreatorViewModel(fileDialog, kitSession, engine);
            desktop.MainWindow.DataContext = viewModel;
            desktop.ShutdownRequested += (_, _) =>
            {
                viewModel.Dispose();
                kitPlayer.Dispose();
                engine.Dispose();
            };

            if (desktop.Args is { Length: > 0 } args && File.Exists(args[0]))
            {
                viewModel.OpenPreset(args[0]);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
