using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SZExtractorGUI.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolean && parameter is string colors)
            {
                var colorStrings = colors.Split(',');
                if (colorStrings.Length == 2)
                {
                    string colorString = boolean ? colorStrings[0] : colorStrings[1];
                    var brush = new BrushConverter().ConvertFromString(colorString) as Brush;
                    return brush ?? Brushes.Black;
                }
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
