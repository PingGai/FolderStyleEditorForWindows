using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Linq;

namespace WindowsFolderStyleEditor_Avalonia
{
    public class MathConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not double doubleValue)
                return null;

            if (parameter is not string expression)
                return null;

            // Simple expression support: @VALUE/2
            if (expression.Contains("@VALUE"))
            {
                string replaced = expression.Replace("@VALUE", doubleValue.ToString(CultureInfo.InvariantCulture));
                var parts = replaced.Split('/');
                if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double num) && double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double den))
                {
                    return num / den;
                }
            }

            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}