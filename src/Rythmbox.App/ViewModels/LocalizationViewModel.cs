using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.App.Localization;

namespace Rythmbox.App.ViewModels;

public sealed partial class LocalizationViewModel : ViewModelBase
{
    private readonly LocalizationService _i18n;

    public LocalizationViewModel(LocalizationService i18n)
    {
        _i18n = i18n;
        _i18n.LanguageChanged += (_, _) => RefreshAll();
        RefreshAll();
    }

    public AppLanguage Language => _i18n.Language;

    public bool IsEnglish => Language == AppLanguage.English;

    public bool IsThai => Language == AppLanguage.Thai;

    [ObservableProperty] private string _navMachine = string.Empty;
    [ObservableProperty] private string _navPads = string.Empty;
    [ObservableProperty] private string _navMixer = string.Empty;
    [ObservableProperty] private string _navEditor = string.Empty;
    [ObservableProperty] private string _navMacro = string.Empty;
    [ObservableProperty] private string _navSettings = string.Empty;
    [ObservableProperty] private string _navScan = string.Empty;
    [ObservableProperty] private string _navSubOutput = string.Empty;

    [ObservableProperty] private string _headerStyle = string.Empty;
    [ObservableProperty] private string _headerPattern = string.Empty;
    [ObservableProperty] private string _headerKit = string.Empty;
    [ObservableProperty] private string _headerTempo = string.Empty;
    [ObservableProperty] private string _headerBackend = string.Empty;
    [ObservableProperty] private string _headerDevice = string.Empty;
    [ObservableProperty] private string _headerScan = string.Empty;
    [ObservableProperty] private string _headerSettingsTip = string.Empty;

    [ObservableProperty] private string _transportTitle = string.Empty;
    [ObservableProperty] private string _transportLoop = string.Empty;
    [ObservableProperty] private string _transportMixer = string.Empty;
    [ObservableProperty] private string _transportScan = string.Empty;
    [ObservableProperty] private string _transportQuit = string.Empty;
    [ObservableProperty] private string _transportTapTempo = string.Empty;
    [ObservableProperty] private string _transportTimeSig = string.Empty;

    [ObservableProperty] private string _headerTapTempo = string.Empty;

    [ObservableProperty] private string _arrangerTitle = string.Empty;
    [ObservableProperty] private string _arrangerStyle = string.Empty;
    [ObservableProperty] private string _arrangerPattern = string.Empty;
    [ObservableProperty] private string _arrangerMacros = string.Empty;
    [ObservableProperty] private string _arrangerComplexity = string.Empty;
    [ObservableProperty] private string _arrangerEnergy = string.Empty;
    [ObservableProperty] private string _arrangerSwing = string.Empty;
    [ObservableProperty] private string _arrangerHumanize = string.Empty;
    [ObservableProperty] private string _arrangerSongChain = string.Empty;
    [ObservableProperty] private string _arrangerSongChainSoon = string.Empty;

    [ObservableProperty] private string _drumPadsTitle = string.Empty;
    [ObservableProperty] private string _drumPadsVoices = string.Empty;

    [ObservableProperty] private string _styleBankTitle = string.Empty;
    [ObservableProperty] private string _styleBankRescan = string.Empty;
    [ObservableProperty] private string _styleBankSearch = string.Empty;

    [ObservableProperty] private string _patternPadsTitle = string.Empty;
    [ObservableProperty] private string _patternPadsCurrent = string.Empty;

    [ObservableProperty] private string _mixerTitle = string.Empty;
    [ObservableProperty] private string _mixerRefresh = string.Empty;
    [ObservableProperty] private string _mixerMachine = string.Empty;
    [ObservableProperty] private string _mixerSelectOutput = string.Empty;

    [ObservableProperty] private string _settingsTitle = string.Empty;
    [ObservableProperty] private string _settingsSubtitle = string.Empty;
    [ObservableProperty] private string _settingsLanguage = string.Empty;
    [ObservableProperty] private string _settingsLanguageDesc = string.Empty;
    [ObservableProperty] private string _settingsLangEnglish = string.Empty;
    [ObservableProperty] private string _settingsLangThai = string.Empty;
    [ObservableProperty] private string _settingsMidiController = string.Empty;
    [ObservableProperty] private string _settingsKeyboardMapping = string.Empty;
    [ObservableProperty] private string _settingsPlayStop = string.Empty;
    [ObservableProperty] private string _settingsMappingOptions = string.Empty;
    [ObservableProperty] private string _settingsMappingHelp = string.Empty;
    [ObservableProperty] private string _settingsToggleMidi = string.Empty;
    [ObservableProperty] private string _settingsPrevPort = string.Empty;
    [ObservableProperty] private string _settingsNextPort = string.Empty;
    [ObservableProperty] private string _settingsSwitchMode = string.Empty;
    [ObservableProperty] private string _settingsLearn = string.Empty;
    [ObservableProperty] private string _settingsKeyNext = string.Empty;
    [ObservableProperty] private string _settingsFootSwitch = string.Empty;
    [ObservableProperty] private string _settingsFootSwitchHelp = string.Empty;
    [ObservableProperty] private string _settingsFootSwitchCcPrev = string.Empty;
    [ObservableProperty] private string _settingsFootSwitchCcNext = string.Empty;
    [ObservableProperty] private string _settingsFootSwitchCycleSig = string.Empty;

    [ObservableProperty] private string _machineExportMidi = string.Empty;

    public string Format(string key, params object[] args) => _i18n.Format(key, args);

    public string Get(string key) => _i18n.Get(key);

    [RelayCommand]
    private void SetEnglish() => SetLanguage(AppLanguage.English);

    [RelayCommand]
    private void SetThai() => SetLanguage(AppLanguage.Thai);

    private void SetLanguage(AppLanguage language)
    {
        _i18n.SetLanguage(language);
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(IsThai));
    }

    private void RefreshAll()
    {
        NavMachine = _i18n["nav.machine"];
        NavPads = _i18n["nav.pads"];
        NavMixer = _i18n["nav.mixer"];
        NavEditor = _i18n["nav.editor"];
        NavMacro = _i18n["nav.macro"];
        NavSettings = _i18n["nav.settings"];
        NavScan = _i18n["nav.scan"];
        NavSubOutput = _i18n["nav.subOutput"];

        HeaderStyle = _i18n["header.style"];
        HeaderPattern = _i18n["header.pattern"];
        HeaderKit = _i18n["header.kit"];
        HeaderTempo = _i18n["header.tempo"];
        HeaderBackend = _i18n["header.backend"];
        HeaderDevice = _i18n["header.device"];
        HeaderScan = _i18n["header.scan"];
        HeaderSettingsTip = _i18n["header.settingsTip"];

        TransportTitle = _i18n["transport.title"];
        TransportLoop = _i18n["transport.loop"];
        TransportMixer = _i18n["transport.mixer"];
        TransportScan = _i18n["transport.scan"];
        TransportQuit = _i18n["transport.quit"];
        TransportTapTempo = _i18n["transport.tapTempo"];
        TransportTimeSig = _i18n["transport.timeSig"];

        HeaderTapTempo = _i18n["header.tapTempo"];

        ArrangerTitle = _i18n["arranger.title"];
        ArrangerStyle = _i18n["arranger.style"];
        ArrangerPattern = _i18n["arranger.pattern"];
        ArrangerMacros = _i18n["arranger.macros"];
        ArrangerComplexity = _i18n["arranger.complexity"];
        ArrangerEnergy = _i18n["arranger.energy"];
        ArrangerSwing = _i18n["arranger.swing"];
        ArrangerHumanize = _i18n["arranger.humanize"];
        ArrangerSongChain = _i18n["arranger.songChain"];
        ArrangerSongChainSoon = _i18n["arranger.songChainSoon"];

        DrumPadsTitle = _i18n["drumPads.title"];
        DrumPadsVoices = _i18n["drumPads.voices"];

        StyleBankTitle = _i18n["styleBank.title"];
        StyleBankRescan = _i18n["styleBank.rescan"];
        StyleBankSearch = _i18n["styleBank.search"];

        PatternPadsTitle = _i18n["patternPads.title"];
        PatternPadsCurrent = _i18n["patternPads.current"];

        MixerTitle = _i18n["mixer.title"];
        MixerRefresh = _i18n["mixer.refresh"];
        MixerMachine = _i18n["mixer.machine"];
        MixerSelectOutput = _i18n["mixer.selectOutput"];

        SettingsTitle = _i18n["settings.title"];
        SettingsSubtitle = _i18n["settings.subtitle"];
        SettingsLanguage = _i18n["settings.language"];
        SettingsLanguageDesc = _i18n["settings.languageDesc"];
        SettingsLangEnglish = _i18n["settings.langEnglish"];
        SettingsLangThai = _i18n["settings.langThai"];
        SettingsMidiController = _i18n["settings.midiController"];
        SettingsKeyboardMapping = _i18n["settings.keyboardMapping"];
        SettingsPlayStop = _i18n["settings.playStop"];
        SettingsMappingOptions = _i18n["settings.mappingOptions"];
        SettingsMappingHelp = _i18n["settings.mappingHelp"];
        SettingsToggleMidi = _i18n["settings.toggleMidi"];
        SettingsPrevPort = _i18n["settings.prevPort"];
        SettingsNextPort = _i18n["settings.nextPort"];
        SettingsSwitchMode = _i18n["settings.switchMode"];
        SettingsLearn = _i18n["settings.learn"];
        SettingsKeyNext = _i18n["settings.keyNext"];
        SettingsFootSwitch = _i18n["settings.footSwitch"];
        SettingsFootSwitchHelp = _i18n["settings.footSwitchHelp"];
        SettingsFootSwitchCcPrev = _i18n["settings.footSwitchCcPrev"];
        SettingsFootSwitchCcNext = _i18n["settings.footSwitchCcNext"];
        SettingsFootSwitchCycleSig = _i18n["settings.footSwitchCycleSig"];

        MachineExportMidi = _i18n["machine.exportMidi"];
    }
}
