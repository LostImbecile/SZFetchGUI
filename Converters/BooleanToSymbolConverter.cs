using System;
using System.Globalization;
using System.Windows.Data;

namespace SZExtractorGUI.Converters
{
    public class BooleanToSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "✓" : "❌"; // Checkmark and X symbols
            }
            return string.Empty; // Default if not a boolean
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}