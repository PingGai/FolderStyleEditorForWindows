using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace FolderStyleEditerForWindows.ValueConverters
{
    public class BoolToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
            {
                // 如果为 true，返回高亮画刷
                if (parameter is string brushKey && Application.Current != null && Application.Current.TryFindResource(brushKey, out var resource) && resource is IBrush selectedBrush)
                {
                    return selectedBrush;
                }
                // 默认高亮颜色
                return new SolidColorBrush(Colors.LightGray);
            }
            // 如果为 false，返回透明画刷
            return new SolidColorBrush(Colors.Transparent);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return AvaloniaProperty.UnsetValue;
        }
    }
}