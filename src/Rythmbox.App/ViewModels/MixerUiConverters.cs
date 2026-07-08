using Avalonia.Data.Converters;
using Rythmbox.Core.Models.Mixer;
using System.Globalization;

namespace Rythmbox.App.ViewModels;

public static class MixerUiConverters
{
    public static readonly IValueConverter MeterHeight = new MeterHeightConverter();
    public static readonly IValueConverter SignalOpacity = new SignalOpacityConverter();
    public static readonly IValueConverter IsGroupKind = new EnumKindConverter(MixerChannelKind.Group);
    public static readonly IValueConverter IsMasterKind = new EnumKindConverter(MixerChannelKind.Master);
    public static readonly IValueConverter IsDrumVoiceKind = new EnumKindConverter(MixerChannelKind.DrumVoice);
    public static readonly IValueConverter AccentIsDrum = new AccentConverter("drum");
    public static readonly IValueConverter AccentIsPerc = new AccentConverter("perc");
    public static readonly IValueConverter AccentIsCym = new AccentConverter("cym");
    public static readonly IValueConverter AccentIsMaster = new AccentConverter("master");

    private sealed class MeterHeightConverter : IValueConverter
    {
        private const double MeterMaxHeight = 108;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var level = value is double d ? d : 0;
            return Math.Clamp(level, 0, 1) * MeterMaxHeight;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private sealed class SignalOpacityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? 1.0 : 0.35;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private sealed class EnumKindConverter(MixerChannelKind kind) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is MixerChannelKind k && k == kind;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    private sealed class AccentConverter(string accent) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => string.Equals(value as string, accent, StringComparison.OrdinalIgnoreCase);

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
