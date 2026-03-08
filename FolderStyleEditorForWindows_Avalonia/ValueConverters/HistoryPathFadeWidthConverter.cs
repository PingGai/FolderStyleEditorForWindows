using System;
using System.Globalization;
using System.Collections.Generic;
using Avalonia.Data.Converters;

namespace FolderStyleEditorForWindows.ValueConverters
{
    public class HistoryPathFadeWidthConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
            {
                return 0d;
            }

            if (values[0] is not string path)
            {
                return 0d;
            }

            var tagWidth = values[1] switch
            {
                double width => width,
                _ => 0d
            };

            if (tagWidth <= 0)
            {
                return 0d;
            }

            var (parentPath, _) = FolderStyleEditorForWindows.PathDisplayHelper.ParsePath(path);
            if (string.IsNullOrWhiteSpace(parentPath))
            {
                return 0d;
            }

            var normalizedParentPath = System.IO.Path.GetFullPath(parentPath);
            var rootPath = System.IO.Path.GetPathRoot(normalizedParentPath) ?? string.Empty;
            var isRootParent = string.Equals(
                normalizedParentPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar),
                rootPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);

            return isRootParent ? tagWidth * 0.5 : tagWidth;
        }
    }
}
