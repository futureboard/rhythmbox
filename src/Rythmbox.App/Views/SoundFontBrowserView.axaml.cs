using Avalonia.Controls;
using Rythmbox.App.ViewModels;

namespace Rythmbox.App.Views;

public partial class SoundFontBrowserView : UserControl
{
    public SoundFontBrowserView()
    {
        InitializeComponent();
        Keyboard.NoteOn += (_, note) => (DataContext as SoundFontBrowserViewModel)?.PlayNote(note);
        Keyboard.NoteOff += (_, note) => (DataContext as SoundFontBrowserViewModel)?.StopNote(note);
    }
}
