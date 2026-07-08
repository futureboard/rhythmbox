using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;

namespace Rythmbox.App.ViewModels;

public sealed partial class PadMappingEntryViewModel : ViewModelBase
{
    private readonly PadMappingService _mapping;
    private readonly Action _refreshParent;

    public PadMappingEntryViewModel(int padIndex, string label, PadMappingService mapping, Action refreshParent)
    {
        PadIndex = padIndex;
        Label = label;
        _mapping = mapping;
        _refreshParent = refreshParent;
    }

    public int PadIndex { get; }

    public string Label { get; }

    public string KeyboardKey => _mapping.GetKeyboardKeyName(PadIndex);

    public string MidiNoteLabel => _mapping.GetMidiNote(PadIndex).ToString();

    public bool IsLearning => _mapping.LearnPadIndex == PadIndex;

    [RelayCommand]
    private void ToggleLearn()
    {
        _mapping.LearnPadIndex = IsLearning ? null : PadIndex;
        _refreshParent();
    }

    [RelayCommand]
    private void DecrementNote()
    {
        _mapping.NudgeMidiNote(PadIndex, -1);
        _refreshParent();
    }

    [RelayCommand]
    private void IncrementNote()
    {
        _mapping.NudgeMidiNote(PadIndex, 1);
        _refreshParent();
    }

    [RelayCommand]
    private void CycleKey()
    {
        _mapping.CycleKeyboardKey(PadIndex);
        _refreshParent();
    }
}

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly PadMappingService _mapping;
    private readonly PadMidiRouter _padRouter;
    private readonly MidiInputService _midiInput;
    private readonly StatusViewModel _status;

    public SettingsViewModel(
        PadMappingService mapping,
        PadMidiRouter padRouter,
        MidiInputService midiInput,
        StatusViewModel status)
    {
        _mapping = mapping;
        _padRouter = padRouter;
        _midiInput = midiInput;
        _status = status;

        RefreshMappings();
        _padRouter.PadLearnCompleted += (_, note) =>
        {
            _status.Show($"MIDI note {note} learned");
            RefreshMappings();
        };
    }

    public ObservableCollection<PadMappingEntryViewModel> Mappings { get; } = new();

    public bool MidiEnabled
    {
        get => _padRouter.IsEnabled;
        set
        {
            _padRouter.IsEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MidiEnabledLabel));
        }
    }

    public string MidiEnabledLabel => MidiEnabled ? "Input: enabled" : "Input: disabled";

    public string MidiPortLabel => _midiInput.IsConnected
        ? $"Port: {_midiInput.ConnectedDevice?.Name ?? "Connected"}"
        : "Port: None";

    public string KeyboardModeLabel => _mapping.UseHomeRowMapping
        ? "Pad mode: QWERTY home row"
        : "Pad mode: Number row 1-8+";

    [RelayCommand]
    private void ToggleMidi()
    {
        MidiEnabled = !MidiEnabled;
        _status.Show(MidiEnabled ? "MIDI input enabled" : "MIDI input disabled");
    }

    [RelayCommand]
    private void PreviousPort()
    {
        if (_midiInput.ConnectPrevious(_padRouter))
        {
            OnPropertyChanged(nameof(MidiPortLabel));
            _status.Show($"MIDI port: {_midiInput.ConnectedDevice?.Name}");
        }
        else
        {
            _status.Show("No MIDI input ports found");
        }
    }

    [RelayCommand]
    private void NextPort()
    {
        if (_midiInput.ConnectNext(_padRouter))
        {
            OnPropertyChanged(nameof(MidiPortLabel));
            _status.Show($"MIDI port: {_midiInput.ConnectedDevice?.Name}");
        }
        else
        {
            _status.Show("No MIDI input ports found");
        }
    }

    [RelayCommand]
    private void ToggleKeyboardMode()
    {
        _mapping.ToggleKeyboardMapping();
        OnPropertyChanged(nameof(KeyboardModeLabel));
        RefreshMappings();
        _status.Show("Keyboard pad mapping updated");
    }

    public void RefreshMappings()
    {
        Mappings.Clear();
        foreach (var pad in GmPercussionMap.Pads)
        {
            Mappings.Add(new PadMappingEntryViewModel(pad.Index, pad.Label, _mapping, RefreshMappings));
        }
    }
}
