using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Linq;

namespace FolderStyleEditorForWindows
{
    public class MathConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not double doubleValue || parameter is not string expression || string.IsNullOrWhiteSpace(expression))
            {
                return null;
            }

            string replaced = expression.Replace("@VALUE", doubleValue.ToString(CultureInfo.InvariantCulture));
            var tokens = replaced.Split(new[] { '*', '/' }, StringSplitOptions.RemoveEmptyEntries);
            var operators = replaced.Where(ch => ch == '*' || ch == '/').ToArray();

            if (tokens.Length == 0)
            {
                return null;
            }

            if (!double.TryParse(tokens[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
            {
                return null;
            }

            for (int i = 0; i < operators.Length && i + 1 < tokens.Length; i++)
            {
                if (!double.TryParse(tokens[i + 1], NumberStyles.Any, CultureInfo.InvariantCulture, out double operand))
                {
                    return null;
                }

                if (operators[i] == '*')
                {
                    result *= operand;
                }
                else
                {
                    if (Math.Abs(operand) < double.Epsilon) return null;
                    result /= operand;
                }
            }

            return result;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
