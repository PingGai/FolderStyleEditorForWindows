using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia;

namespace FolderStyleEditorForWindows.ViewModels
{
    public partial class HoverIconViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isVisible;

        [ObservableProperty]
        private Point _position;

        [ObservableProperty]
        private string? _mainIconPath;

        [ObservableProperty]
        private string? _mainIconPngPath;

        [ObservableProperty]
        private string? _badgeIconPath;
    }
}