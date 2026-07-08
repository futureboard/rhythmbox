using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.App.Localization;
using Rythmbox.Core.Models.Styles;
using Rythmbox.Core.Styles;

namespace Rythmbox.App.ViewModels;

public sealed partial class StyleBankViewModel : ViewModelBase
{
    public const int PadCount = 9;

    private readonly StyleBankService _styleBank;
    private readonly Action<StyleDefinition> _onStyleSelected;
    private readonly LocalizationService _i18n;
    private List<StyleDefinition> _allStyles = [];
    private string? _stylesRoot;
    private bool _syncingSelection;

    public StyleBankViewModel(
        StyleBankService styleBank,
        Action<StyleDefinition> onStyleSelected,
        LocalizationService i18n)
    {
        _styleBank = styleBank;
        _onStyleSelected = onStyleSelected;
        _i18n = i18n;
        _i18n.LanguageChanged += OnLanguageChanged;

        for (var slot = 1; slot <= PadCount; slot++)
        {
            StylePads.Add(new StyleBankPadViewModel(slot, OnPadSelected, _i18n));
        }

        UpdateEmptyMessage();
    }

    public ObservableCollection<string> Categories { get; } = new();

    public ObservableCollection<StyleListItemViewModel> Styles { get; } = new();

    public ObservableCollection<StyleBankPadViewModel> StylePads { get; } = new();

    [ObservableProperty]
    private string? _selectedCategory;

    [ObservableProperty]
    private StyleListItemViewModel? _selectedStyle;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _emptyMessage = string.Empty;

    partial void OnSelectedCategoryChanged(string? value) => ApplyFilter();

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedStyleChanged(StyleListItemViewModel? value)
    {
        if (_syncingSelection)
        {
            return;
        }

        if (value?.Style is { IsValid: true } style)
        {
            _onStyleSelected(style);
        }
    }

    public void Scan(string? stylesRoot)
    {
        _stylesRoot = stylesRoot;
        _styleBank.Scan(stylesRoot);
        _allStyles = _styleBank.AllStyles.ToList();
        PopulatePads();

        Categories.Clear();
        foreach (var cat in _styleBank.Categories)
        {
            Categories.Add(cat);
        }

        SelectedCategory ??= Categories.FirstOrDefault();
        UpdateEmptyMessage();
        ApplyFilter();
    }

    public void RefreshLocalizedLabels()
    {
        UpdateEmptyMessage();
        foreach (var pad in StylePads)
        {
            pad.RefreshLocalizedLabels();
        }

        foreach (var style in Styles)
        {
            style.RefreshLocalizedLabels();
        }
    }

    private void UpdateEmptyMessage()
    {
        EmptyMessage = _allStyles.Count == 0
            ? _i18n["styleBank.noStyles"]
            : _i18n.Format("styleBank.stylesCount", _allStyles.Count);
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RefreshLocalizedLabels();

    public void SyncFromSession(ArrangerSession session)
    {
        var selectedId = session.SelectedStyle?.Id;
        foreach (var pad in StylePads)
        {
            pad.SyncSelection(selectedId);
        }

        _syncingSelection = true;
        SelectedStyle = Styles.FirstOrDefault(s =>
            selectedId is not null
            && string.Equals(s.Style.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        _syncingSelection = false;
    }

    [RelayCommand]
    private void Rescan() => Scan(_stylesRoot);

    private void OnPadSelected(StyleBankPadViewModel pad)
    {
        if (pad.AssignedStyle is not { IsValid: true } style)
        {
            return;
        }

        _onStyleSelected(style);

        _syncingSelection = true;
        SelectedStyle = Styles.FirstOrDefault(s =>
            string.Equals(s.Style.Id, style.Id, StringComparison.OrdinalIgnoreCase));
        _syncingSelection = false;
    }

    private void PopulatePads()
    {
        var validStyles = _allStyles.Where(s => s.IsValid).ToList();

        for (var i = 0; i < StylePads.Count; i++)
        {
            StylePads[i].Assign(i < validStyles.Count ? validStyles[i] : null);
        }
    }

    private void ApplyFilter()
    {
        Styles.Clear();

        IEnumerable<StyleDefinition> query = _allStyles;

        if (!string.IsNullOrWhiteSpace(SelectedCategory))
        {
            query = query.Where(s => string.Equals(s.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(s =>
                s.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || s.Tags.Any(t => t.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var style in query)
        {
            Styles.Add(new StyleListItemViewModel(style, _i18n));
        }
    }
}

public sealed partial class StyleListItemViewModel : ViewModelBase
{
    private readonly LocalizationService _i18n;

    public StyleListItemViewModel(StyleDefinition style, LocalizationService i18n)
    {
        Style = style;
        _i18n = i18n;
    }

    public StyleDefinition Style { get; }

    public string Name => Style.Name;

    public string Category => Style.Category;

    public string Detail => $"{Style.DefaultTempo:0} BPM · {Style.TimeSignature}";

    public bool HasErrors => !Style.IsValid;

    public string StatusLabel => HasErrors
        ? _i18n["style.status.invalid"]
        : Style.ValidationWarnings.Count > 0
            ? _i18n["style.status.incomplete"]
            : _i18n["style.status.ready"];

    public void RefreshLocalizedLabels() => OnPropertyChanged(nameof(StatusLabel));
}
