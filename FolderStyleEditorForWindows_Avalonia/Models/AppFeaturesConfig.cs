using System.Collections.Generic;

namespace FolderStyleEditorForWindows.Models
{
    public class AppFeaturesConfig
    {
        public FeaturesConfig Features { get; set; } = new();
        public HoverIconConfig HoverIcon { get; set; } = new();
        public PinIconConfig PinIcon { get; set; } = new();
       public AnimationsConfig Animations { get; set; } = new();
    }

    public class FeaturesConfig
    {
        public int PinDoubleClickThreshold { get; set; } = 500;
    }

    public class HoverIconConfig
    {
        public string DefaultIcon { get; set; } = "";
        public string ErrorIcon { get; set; } = "";
        public FileTypesConfig FileTypes { get; set; } = new();
        public List<MainIconRule> MainIcons { get; set; } = new();
        public List<BadgeIconRule> BadgeIcons { get; set; } = new();
    }

    public class FileTypesConfig
    {
        public List<string> Supported { get; set; } = new();
        public List<string> SupportedToConvert { get; set; } = new();
    }

    public class MainIconRule
    {
        public List<string> Extensions { get; set; } = new();
        public string IconPath { get; set; } = "";
    }

    public class BadgeIconRule
    {
        public string Status { get; set; } = "";
        public string IconPath { get; set; } = "";
    }

    public class PinIconConfig
    {
        public string MainIcon { get; set; } = "";
        public string TestPngPath { get; set; } = "";
        public List<PinBadgeIconRule> BadgeIcons { get; set; } = new();
    }

    public class PinBadgeIconRule
    {
        public string State { get; set; } = "";
        public string IconPath { get; set; } = "";
    }

   public class AnimationsConfig
   {
       public int ToastAnimationDuration { get; set; } = 300; // in milliseconds
   }

    public class AppearanceConfig
    {
        public string? SvgDefaultColor { get; set; }
    }
}