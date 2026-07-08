using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Rythmbox.App.Views;

/// <summary>
/// A minimal on-screen piano (two octaves) used to audition SoundFont presets without
/// requiring a physical MIDI keyboard. Notes are reported via <see cref="NoteOn"/>/<see cref="NoteOff"/>.
/// </summary>
public partial class PianoKeyboardControl : UserControl
{
    private const int StartNote = 48; // C3
    private const int OctaveCount = 2;
    private const double WhiteKeyWidth = 34;
    private const double WhiteKeyHeight = 120;
    private const double BlackKeyWidth = 22;
    private const double BlackKeyHeight = 74;

    // Semitone offset from C for each white key, and the black key that follows it (-1 = none).
    private static readonly int[] WhiteOffsets = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] BlackOffsets = [1, 3, -1, 6, 8, 10, -1];

    private static readonly IBrush WhiteKeyBrush = Brushes.WhiteSmoke;
    private static readonly IBrush BlackKeyBrush = Brushes.Black;
    private static readonly IBrush PressedKeyBrush = new SolidColorBrush(Color.Parse("#FF8C1A"));

    public event EventHandler<int>? NoteOn;

    public event EventHandler<int>? NoteOff;

    public PianoKeyboardControl()
    {
        InitializeComponent();
        BuildKeyboard();
    }

    private void BuildKeyboard()
    {
        const int whiteKeysPerOctave = 7;
        var totalWhiteKeys = whiteKeysPerOctave * OctaveCount + 1;

        KeyboardCanvas.Width = totalWhiteKeys * WhiteKeyWidth;
        KeyboardCanvas.Height = WhiteKeyHeight;

        for (var i = 0; i < totalWhiteKeys; i++)
        {
            var octave = i / whiteKeysPerOctave;
            var indexInOctave = i % whiteKeysPerOctave;
            var note = StartNote + (octave * 12) + WhiteOffsets[indexInOctave];

            var key = CreateKey(WhiteKeyWidth, WhiteKeyHeight, isBlack: false, note);
            Canvas.SetLeft(key, i * WhiteKeyWidth);
            Canvas.SetTop(key, 0);
            KeyboardCanvas.Children.Add(key);
        }

        for (var octave = 0; octave < OctaveCount; octave++)
        {
            for (var indexInOctave = 0; indexInOctave < BlackOffsets.Length; indexInOctave++)
            {
                if (BlackOffsets[indexInOctave] < 0)
                {
                    continue;
                }

                var note = StartNote + (octave * 12) + BlackOffsets[indexInOctave];
                var key = CreateKey(BlackKeyWidth, BlackKeyHeight, isBlack: true, note);

                var left = ((octave * whiteKeysPerOctave) + indexInOctave + 1) * WhiteKeyWidth - (BlackKeyWidth / 2);
                Canvas.SetLeft(key, left);
                Canvas.SetTop(key, 0);
                KeyboardCanvas.Children.Add(key);
            }
        }
    }

    private Border CreateKey(double width, double height, bool isBlack, int note)
    {
        var restBrush = isBlack ? BlackKeyBrush : WhiteKeyBrush;

        var border = new Border
        {
            Width = width,
            Height = height,
            Background = restBrush,
            BorderBrush = Brushes.Black,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(0, 0, 4, 4),
            ZIndex = isBlack ? 1 : 0,
        };

        var isPressed = false;

        void Press()
        {
            if (isPressed)
            {
                return;
            }

            isPressed = true;
            border.Background = PressedKeyBrush;
            NoteOn?.Invoke(this, note);
        }

        void Release()
        {
            if (!isPressed)
            {
                return;
            }

            isPressed = false;
            border.Background = restBrush;
            NoteOff?.Invoke(this, note);
        }

        border.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            Press();
        };
        border.PointerReleased += (_, _) => Release();
        border.PointerCaptureLost += (_, _) => Release();
        border.PointerExited += (_, e) =>
        {
            if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            {
                Release();
            }
        };

        return border;
    }
}
