using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FolderStyleEditorForWindows.ValueConverters
{
    /// <summary>
    /// 根据是否存在第二个按钮，返回主按钮/次按钮宽度。
    /// </summary>
    public class DialogButtonWidthConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2) return null;

            if (values[0] is not double cardWidth || values[1] is not bool hasSecondary)
            {
                return null;
            }

            var ratio = hasSecondary ? 0.38 : 0.55;
            return cardWidth * ratio;
        }
    }
}
