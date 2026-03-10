using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FolderStyleEditorForWindows.ValueConverters
{
    public class DialogWidthRatioConverter : IMultiValueConverter
    {
        private const double BaseWidthRatio = 0.64;

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2 || values[0] is not double windowWidth)
            {
                return null;
            }

            var widthRatio = values[1] is double ratio && ratio > 0 ? ratio : 1.0;
            return windowWidth * BaseWidthRatio * widthRatio;
        }
    }
}
