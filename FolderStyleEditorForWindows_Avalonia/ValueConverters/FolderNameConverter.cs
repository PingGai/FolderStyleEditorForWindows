using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace FolderStyleEditorForWindows.ValueConverters
{
    public sealed class FolderNameConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string path)
            {
                var (_, folderName) = FolderStyleEditorForWindows.PathDisplayHelper.ParsePath(path);
                return FolderStyleEditorForWindows.PathDisplayHelper.TruncateFolderName(folderName);
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return AvaloniaProperty.UnsetValue;
        }
    }
}
