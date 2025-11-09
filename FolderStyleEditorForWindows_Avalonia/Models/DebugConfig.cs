namespace FolderStyleEditorForWindows.Models
{
    public class DebugConfig
    {
        public bool EnableOverlay { get; set; } = true;
        public bool ShowHoverIconBackgroundColor { get; set; } = true;
        public string HoverIconBgColor { get; set; } = "#DD4444";
        public bool ShowSvgTest { get; set; } = true;
        public bool ShowPngTest { get; set; } = true;
        public bool ShowHoverIconClone { get; set; } = true;
        public string SvgTestPath { get; set; } = "avares://FolderStyleEditorForWindows/Resources/SVG/book-image.svg";
        public string PngTestPath { get; set; } = "avares://FolderStyleEditorForWindows/Resources/PNG/test-pin.png";
        public double SvgOpacity { get; set; } = 0.75;
    }
}