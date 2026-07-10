using Avalonia.Controls;

namespace Rythmbox.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => RootView.DisposeRuntime();
    }

    /// <summary>Kept for API compatibility with the application lifetime wiring.</summary>
    public void DisposeRuntime() => RootView.DisposeRuntime();

    public Task InitializeRuntimeAsync() => RootView.InitializeRuntimeAsync();
}
