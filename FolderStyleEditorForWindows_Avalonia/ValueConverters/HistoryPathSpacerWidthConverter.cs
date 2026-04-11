using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FolderStyleEditorForWindows.ValueConverters
{
    public sealed class HistoryPathSpacerWidthConverter : IMultiValueConverter
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

            var listWidth = values.Count > 2
                ? values[2] switch
                {
                    double width => width,
                    _ => 0d
                }
                : 0d;

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
            var relativeParentPath = normalizedParentPath.Length > rootPath.Length
                ? normalizedParentPath[rootPath.Length..].Trim(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
                : string.Empty;
            var parentDepth = string.IsNullOrWhiteSpace(relativeParentPath)
                ? 0
                : relativeParentPath.Split(
                    new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar },
                    StringSplitOptions.RemoveEmptyEntries).Length;

            var baseSpacerWidth = parentDepth switch
            {
                0 => 26d,
                1 => 9d,
                2 => 3d,
                _ => 0d
            };

            if (listWidth <= 0)
            {
                return baseSpacerWidth;
            }

            var estimatedParentPathWidth = EstimatePathWidth(parentPath, 7.1);
            var textTrailingMargin = 6d;
            var rowChromeReserve = isRootParent ? 24d : 32d;
            var availableTextAndSpacerWidth = Math.Max(0d, listWidth - tagWidth - rowChromeReserve);
            var remainingHeadroom = availableTextAndSpacerWidth - estimatedParentPathWidth - textTrailingMargin;

            if (remainingHeadroom <= 0)
            {
                return 0d;
            }

            if (baseSpacerWidth <= 0)
            {
                return 0d;
            }

            var usableSpacerWidth = remainingHeadroom - 2d;
            if (usableSpacerWidth <= 0)
            {
                return 0d;
            }

            return Math.Clamp(Math.Min(baseSpacerWidth, usableSpacerWidth), 0d, 26d);
        }

        private static double EstimatePathWidth(string path, double charWidth)
        {
            if (string.IsNullOrEmpty(path))
            {
                return 0d;
            }

            var width = 0d;
            foreach (var ch in path)
            {
                width += ch <= 0x7F ? charWidth : charWidth * 1.65;
            }

            return width;
        }
    }
}
