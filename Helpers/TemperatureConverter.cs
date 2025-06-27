// TemperatureConverter.cs - perbaiki dengan koreksi nilai
using System.Globalization;

namespace MauiApp2.Helpers
{
    public class TemperatureConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "0,0 °C";

            try
            {
                // Coba konversi ke double
                double temp = System.Convert.ToDouble(value);

                // Koreksi nilai yang terlalu besar (misal 206.0 seharusnya 20.6)
                if (temp > 100 && temp <= 500)
                {
                    double corrected = temp / 10.0;
                    return corrected.ToString("0.0", new CultureInfo("id-ID")) + " °C";
                }

                return temp.ToString("0.0", new CultureInfo("id-ID")) + " °C";
            }
            catch
            {
                return "ERROR";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}