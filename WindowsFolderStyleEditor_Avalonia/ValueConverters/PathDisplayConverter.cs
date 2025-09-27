using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace WindowsFolderStyleEditor_Avalonia.ValueConverters
{
    public class PathDisplayConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string path)
            {
                var (parentPath, folderName) = PathDisplayHelper.ParsePath(path);
                return parentPath;
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}