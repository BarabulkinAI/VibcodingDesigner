using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ReportDesigner.UI.Converters;

/// <summary>Bool → FontWeight (Bold/Normal) — для живого предпросмотра шрифта в поле
/// редактирования текста на панели свойств (см. PropertiesPanelView, «Текст»).</summary>
public class BoolToFontWeightConverter : IValueConverter
{
    public static readonly BoolToFontWeightConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? FontWeight.Bold : FontWeight.Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Bool → FontStyle (Italic/Normal) — тот же предпросмотр, что и <see cref="BoolToFontWeightConverter"/>.</summary>
public class BoolToFontStyleConverter : IValueConverter
{
    public static readonly BoolToFontStyleConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? FontStyle.Italic : FontStyle.Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
