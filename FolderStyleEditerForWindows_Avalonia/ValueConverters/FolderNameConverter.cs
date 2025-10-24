using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FolderStyleEditerForWindows.ValueConverters
{
    public class FolderNameConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string path)
            {
                var (parentPath, folderName) = FolderStyleEditerForWindows.PathDisplayHelper.ParsePath(path);
                return FolderStyleEditerForWindows.PathDisplayHelper.TruncateFolderName(folderName);
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}