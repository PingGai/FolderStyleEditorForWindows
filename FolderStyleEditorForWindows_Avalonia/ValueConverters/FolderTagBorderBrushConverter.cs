using System;
using System.Collections.Generic;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FolderStyleEditorForWindows.Services;

namespace FolderStyleEditorForWindows.ValueConverters
{
    public sealed class FolderTagBorderBrushConverter : IMultiValueConverter
    {
        private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#9CBDE1"));
        private static readonly IBrush ProtectedBrush = new SolidColorBrush(Color.Parse("#D89494"));
        private static readonly IBrush ElevatedProtectedBrush = new SolidColorBrush(Color.Parse("#D4B46D"));

        public object Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (values.Count < 2)
            {
                return DefaultBrush;
            }

            var path = values[0] as string;
            var isElevated = values[1] is bool b && b;
            if (!FolderProtectionPolicy.RequiresElevation(path))
            {
                return DefaultBrush;
            }

            return isElevated ? ElevatedProtectedBrush : ProtectedBrush;
        }
    }
}
