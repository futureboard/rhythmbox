using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.Core.Models.Styles;
using Rythmbox.Core.Styles;

namespace Rythmbox.App.ViewModels;

public sealed partial class StyleBankViewModel : ViewModelBase
{
    private readonly StyleBankService _styleBank;
    private readonly Action<StyleDefinition> _onStyleSelected;
    private List<StyleDefinition> _allStyles = [];
    private string? _stylesRoot;

    public StyleBankViewModel(StyleBankService styleBank, Action<StyleDefinition> onStyleSelected)
    {
        _styleBank = styleBank;
        _onStyleSelected = onStyleSelected;
    }

    public ObservableCollection<string> Categories { get; } = new();

    public ObservableCollection<StyleListItemViewModel> Styles { get; } = new();

    public ObservableCollection<StyleListItemViewModel> RecentStyles { get; } = new();

    [ObservableProperty]
    private string? _selectedCategory;

    [ObservableProperty]
    private StyleListItemViewModel? _selectedStyle;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _emptyMessage = "Choose a style to start";

    partial void OnSelectedCategoryChanged(string? value) => ApplyFilter();

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedStyleChanged(StyleListItemViewModel? value)
    {
        if (value?.Style is { IsValid: true } style)
        {
            _onStyleSelected(style);
            AddRecent(style);
        }
    }

    public void Scan(string? stylesRoot)
    {
        _stylesRoot = stylesRoot;
        _styleBank.Scan(stylesRoot);
        _allStyles = _styleBank.AllStyles.ToList();

        Categories.Clear();
        foreach (var cat in _styleBank.Categories)
        {
            Categories.Add(cat);
        }

        SelectedCategory ??= Categories.FirstOrDefault();
        EmptyMessage = _allStyles.Count == 0
            ? "No styles found — add Content/Styles or browse loops below"
            : "Choose a style to start";

        ApplyFilter();
    }

    [RelayCommand]
    private void Rescan() => Scan(_stylesRoot);

    [RelayCommand]
    private void ToggleFavorite(StyleListItemViewModel item)
    {
        item.IsFavorite = !item.IsFavorite;
        ApplyFilter();
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
            Styles.Add(new StyleListItemViewModel(style));
        }
    }

    private void AddRecent(StyleDefinition style)
    {
        var existing = RecentStyles.FirstOrDefault(r => r.Style.Id == style.Id);
        if (existing is not null)
        {
            RecentStyles.Remove(existing);
        }

        RecentStyles.Insert(0, new StyleListItemViewModel(style));
        while (RecentStyles.Count > 8)
        {
            RecentStyles.RemoveAt(RecentStyles.Count - 1);
        }
    }
}

public sealed partial class StyleListItemViewModel : ViewModelBase
{
    public StyleListItemViewModel(StyleDefinition style)
    {
        Style = style;
    }

    public StyleDefinition Style { get; }

    public string Name => Style.Name;

    public string Category => Style.Category;

    public string Detail => $"{Style.DefaultTempo:0} BPM · {Style.TimeSignature}";

    public bool HasErrors => !Style.IsValid;

    public string StatusLabel => HasErrors
        ? "Invalid"
        : Style.ValidationWarnings.Count > 0
            ? "Incomplete"
            : "Ready";

    [ObservableProperty]
    private bool _isFavorite;
}
