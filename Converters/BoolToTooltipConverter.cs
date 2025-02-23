using System;
using System.Globalization;
using System.Windows.Data;

namespace SZExtractorGUI.Converters
{
    public class BoolToTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEnabled && parameter is string messages)
            {
                var parts = messages.Split(',');
                if (parts.Length == 2)
                {
                    return isEnabled ? parts[0] : parts[1];
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
