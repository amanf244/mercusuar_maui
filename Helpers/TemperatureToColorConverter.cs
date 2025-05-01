using System.Globalization;

namespace MauiApp2.Helpers;

public class TemperatureToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double temp)
        {
            if (temp >= 0 && temp < 37)
            {
                return Colors.Green;
            }
            else if (temp >= 37 && temp < 54)
            {
                return Colors.Orange;
            }
            else if (temp >= 54 && temp <= 100)
            {
                return Colors.Red;
            }
        }

        return Colors.Black;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InvertedBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "Menyala" : "Mati";
        }
        return "Unknown";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
