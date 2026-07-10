using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.App.Localization;
using Rythmbox.Core.Engine;
using Rythmbox.Core.Models;
using Rythmbox.Core.Models.Styles;
using Rythmbox.Core.Styles;

namespace Rythmbox.App.ViewModels;

public sealed partial class PadMappingEntryViewModel : ViewModelBase
{
    private readonly PadMappingService _mapping;
    private readonly LocalizationService _i18n;
    private readonly Action _refreshParent;

    public PadMappingEntryViewModel(
        int padIndex,
        string label,
        PadMappingService mapping,
        LocalizationService i18n,
        Action refreshParent)
    {
        PadIndex = padIndex;
        Label = label;
        _mapping = mapping;
        _i18n = i18n;
        _refreshParent = refreshParent;
    }

    public int PadIndex { get; }

    public string Label { get; }

    public string KeyboardKeyLabel => _i18n.Format("settings.keyLabel", _mapping.GetKeyboardKeyName(PadIndex));

    public string MidiNoteLabel => _i18n.Format("settings.midiLabel", _mapping.GetMidiNote(PadIndex));

    public string LearnLabel => _i18n["settings.learn"];

    public string KeyNextLabel => _i18n["settings.keyNext"];

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

    public void RefreshLabels()
    {
        OnPropertyChanged(nameof(KeyboardKeyLabel));
        OnPropertyChanged(nameof(MidiNoteLabel));
        OnPropertyChanged(nameof(LearnLabel));
        OnPropertyChanged(nameof(KeyNextLabel));
        OnPropertyChanged(nameof(IsLearning));
    }
}

public sealed partial class SettingsViewModel : ViewModelBase
{
    private static readonly TimeSignature[] MomentaryOptions =
    [
        new(2, 4),
        new(3, 4),
        new(6, 8),
        new(5, 4),
    ];

    private readonly PadMappingService _mapping;
    private readonly PadMidiRouter _padRouter;
    private readonly MidiInputService _midiInput;
    private readonly MidiFootSwitchController _footSwitch;
    private readonly PatternArrangerEngine _arranger;
    private readonly StatusViewModel _status;
    private readonly LocalizationService _i18n;

    public SettingsViewModel(
        PadMappingService mapping,
        PadMidiRouter padRouter,
        MidiInputService midiInput,
        MidiFootSwitchController footSwitch,
        PatternArrangerEngine arranger,
        StatusViewModel status,
        LocalizationService i18n)
    {
        _mapping = mapping;
        _padRouter = padRouter;
        _midiInput = midiInput;
        _footSwitch = footSwitch;
        _arranger = arranger;
        _status = status;
        _i18n = i18n;

        _i18n.LanguageChanged += (_, _) => RefreshLocalizedLabels();

        RefreshMappings();
        _padRouter.PadLearnCompleted += (_, note) =>
        {
            _status.Show(_i18n.Format("status.midiLearned", note));
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

    public string MidiEnabledLabel => MidiEnabled
        ? _i18n["status.inputEnabled"]
        : _i18n["status.inputDisabled"];

    public string MidiPortLabel => _midiInput.IsConnected
        ? _i18n.Format("status.portConnected", _midiInput.ConnectedDevice?.Name ?? "Connected")
        : _i18n["status.portNone"];

    public string KeyboardModeLabel => _mapping.UseHomeRowMapping
        ? _i18n["status.padModeHome"]
        : _i18n["status.padModeNumbers"];

    public string FootSwitchCcLabel => _i18n.Format("settings.footSwitchCc", _footSwitch.ControllerNumber);

    public string FootSwitchSignatureLabel =>
        _i18n.Format("settings.footSwitchSig", _arranger.MomentarySignature.ToString());

    [RelayCommand]
    private void FootSwitchCcPrev()
    {
        _footSwitch.ControllerNumber = Math.Clamp(_footSwitch.ControllerNumber - 1, 0, 127);
        OnPropertyChanged(nameof(FootSwitchCcLabel));
    }

    [RelayCommand]
    private void FootSwitchCcNext()
    {
        _footSwitch.ControllerNumber = Math.Clamp(_footSwitch.ControllerNumber + 1, 0, 127);
        OnPropertyChanged(nameof(FootSwitchCcLabel));
    }

    [RelayCommand]
    private void CycleFootSwitchSignature()
    {
        var index = Array.IndexOf(MomentaryOptions, _arranger.MomentarySignature);
        _arranger.MomentarySignature = MomentaryOptions[(index + 1) % MomentaryOptions.Length];
        OnPropertyChanged(nameof(FootSwitchSignatureLabel));
    }

    [RelayCommand]
    private void ToggleMidi()
    {
        MidiEnabled = !MidiEnabled;
        _status.Show(MidiEnabled ? _i18n["status.midiEnabled"] : _i18n["status.midiDisabled"]);
    }

    [RelayCommand]
    private void PreviousPort()
    {
        if (_midiInput.ConnectPrevious(_padRouter))
        {
            RefreshLocalizedLabels();
            _status.Show(_i18n.Format("status.portConnected", _midiInput.ConnectedDevice?.Name ?? string.Empty));
        }
        else
        {
            _status.Show(_i18n["status.noMidiPorts"]);
        }
    }

    [RelayCommand]
    private void NextPort()
    {
        if (_midiInput.ConnectNext(_padRouter))
        {
            RefreshLocalizedLabels();
            _status.Show(_i18n.Format("status.portConnected", _midiInput.ConnectedDevice?.Name ?? string.Empty));
        }
        else
        {
            _status.Show(_i18n["status.noMidiPorts"]);
        }
    }

    [RelayCommand]
    private void ToggleKeyboardMode()
    {
        _mapping.ToggleKeyboardMapping();
        RefreshLocalizedLabels();
        RefreshMappings();
        _status.Show(_i18n["status.keyboardUpdated"]);
    }

    public void RefreshMappings()
    {
        Mappings.Clear();
        foreach (var pad in GmPercussionMap.Pads)
        {
            Mappings.Add(new PadMappingEntryViewModel(pad.Index, pad.Label, _mapping, _i18n, RefreshMappings));
        }
    }

    private void RefreshLocalizedLabels()
    {
        OnPropertyChanged(nameof(MidiEnabledLabel));
        OnPropertyChanged(nameof(MidiPortLabel));
        OnPropertyChanged(nameof(KeyboardModeLabel));
        OnPropertyChanged(nameof(FootSwitchCcLabel));
        OnPropertyChanged(nameof(FootSwitchSignatureLabel));
        foreach (var mapping in Mappings)
        {
            mapping.RefreshLabels();
        }
    }
}
