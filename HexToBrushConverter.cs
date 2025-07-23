using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ICOforge
{
    public class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hexColor)
            {
                try
                {
                    // Use the fully qualified name for the WPF Color and ColorConverter classes
                    return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor));
                }
                catch
                {
                    // Use the fully qualified name for the WPF Colors class
                    return new SolidColorBrush(System.Windows.Media.Colors.White);
                }
            }
            return new SolidColorBrush(System.Windows.Media.Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}