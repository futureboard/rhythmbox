using Avalonia.Controls;
using Avalonia.Input;
using Rythmbox.App.ViewModels;

namespace Rythmbox.App.Views;

public partial class MixerChannelStripView : UserControl
{
    public MixerChannelStripView()
    {
        InitializeComponent();
    }

    private void OnStripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MixerChannelStripViewModel vm)
        {
            vm.SelectCommand.Execute(null);
        }
    }
}
