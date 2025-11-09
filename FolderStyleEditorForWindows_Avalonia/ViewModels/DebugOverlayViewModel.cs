using CommunityToolkit.Mvvm.ComponentModel;
using FolderStyleEditorForWindows.Models;
using FolderStyleEditorForWindows.Services;

namespace FolderStyleEditorForWindows.ViewModels
{
    public partial class DebugOverlayViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isEnabled;

        [ObservableProperty]
        private bool _showSvgTest;

        [ObservableProperty]
        private bool _showPngTest;

        [ObservableProperty]
        private bool _showHoverIconClone;

        public string SvgTestPath => "avares://FolderStyleEditorForWindows_Avalonia/Resources/SVG/book-image.svg";
        public string PngTestPath => "avares://FolderStyleEditorForWindows_Avalonia/Resources/PNG/test-pin.png";

        // This will be bound to the main HoverIconViewModel instance
        public HoverIconViewModel HoverIconClone { get; }

        public DebugOverlayViewModel(AppConfig config, HoverIconViewModel hoverIconClone)
        {
            _isEnabled = config.Debug.EnableOverlay;
            _showSvgTest = config.Debug.ShowSvgTest;
            _showPngTest = config.Debug.ShowPngTest;
            _showHoverIconClone = config.Debug.ShowHoverIconClone;
            HoverIconClone = hoverIconClone;
        }
    }
}