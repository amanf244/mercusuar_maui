using System.Globalization;

namespace MauiApp2.Helpers;

public class BoolToColorConverter : IValueConverter
{
    public Color TrueColor
    {
        get; set;
    }
    public Color FalseColor
    {
        get; set;
    }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? TrueColor : FalseColor;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}