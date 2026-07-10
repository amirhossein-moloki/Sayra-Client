using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Sayra.UI.Controls
{
    // Helper BooleanToVisibilityConverter supporting Inversion & HasValue parameters
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool val = false;

            if (value is bool b)
            {
                val = b;
            }
            else if (value is string s)
            {
                val = !string.IsNullOrEmpty(s);
            }

            if (parameter as string == "Inverse")
            {
                val = !val;
            }

            // Return Visibility or bool depending on target property type to prevent binding type mismatch!
            if (targetType == typeof(Visibility))
            {
                return val ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (targetType == typeof(bool))
            {
                return val;
            }

            return val;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
