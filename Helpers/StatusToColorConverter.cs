using System;
using System.Globalization;
using Microsoft.Maui.Graphics;

namespace MauiApp2.Helpers;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool status)
        {
            return status ? Color.FromArgb("#4CAF50") : Color.FromArgb("#F44336");
        }
        return Color.FromArgb("#607D8B"); // Warna default
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}