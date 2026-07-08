using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rythmbox.App.Localization;
using Rythmbox.Core.Models.Styles;
using Rythmbox.Core.Styles;

namespace Rythmbox.App.ViewModels;

public sealed partial class StyleBankPadViewModel : ViewModelBase
{
    private readonly Action<StyleBankPadViewModel> _onSelect;
    private readonly LocalizationService _i18n;

    public StyleBankPadViewModel(
        int slotNumber,
        Action<StyleBankPadViewModel> onSelect,
        LocalizationService i18n)
    {
        SlotNumber = slotNumber;
        _onSelect = onSelect;
        _i18n = i18n;
    }

    public int SlotNumber { get; }

    public StyleDefinition? AssignedStyle { get; private set; }

    public string Label => AssignedStyle?.Name ?? SlotNumber.ToString();

    public string SubLabel => AssignedStyle is null
        ? _i18n["styleBankPad.empty"]
        : $"{AssignedStyle.DefaultTempo:0} BPM";

    public bool IsEmpty => AssignedStyle is null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectedState))]
    [NotifyPropertyChangedFor(nameof(IsMissingState))]
    private bool _isSelected;

    public bool IsSelectedState => IsSelected;

    public bool IsMissingState => AssignedStyle is { IsValid: false };

    public bool IsEnabled => AssignedStyle is { IsValid: true };

    public string ToolTipText => AssignedStyle switch
    {
        null => _i18n.Format("styleBankPad.tooltipEmpty", SlotNumber),
        { IsValid: false } => _i18n.Format("styleBankPad.tooltipInvalid", AssignedStyle.Name),
        _ => $"{AssignedStyle.Name} · {AssignedStyle.Category}",
    };

    public void Assign(StyleDefinition? style)
    {
        AssignedStyle = style;
        RefreshLocalizedLabels();
    }

    public void RefreshLocalizedLabels()
    {
        OnPropertyChanged(nameof(AssignedStyle));
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(SubLabel));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(ToolTipText));
        OnPropertyChanged(nameof(IsMissingState));
    }

    public void SyncSelection(string? selectedStyleId)
    {
        IsSelected = AssignedStyle is not null
                     && string.Equals(AssignedStyle.Id, selectedStyleId, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void Select()
    {
        if (AssignedStyle is { IsValid: true })
        {
            _onSelect(this);
        }
    }
}
