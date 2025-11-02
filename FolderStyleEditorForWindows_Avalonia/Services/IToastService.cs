using System;
using Avalonia.Media;

namespace FolderStyleEditorForWindows.Services
{
    public interface IToastService
    {
        void Show(string message, SolidColorBrush? color = null, TimeSpan? duration = null);
    }
}