namespace FolderStyleEditorForWindows.Models
{
    public class AppConfig
    {
        public string Language { get; set; } = "en-US";
        public AppFeaturesConfig Features { get; set; } = new AppFeaturesConfig();
        public HoverIconConfig HoverIcon { get; set; } = new HoverIconConfig();
        public PinIconConfig PinIcon { get; set; } = new PinIconConfig();
        public AnimationsConfig Animations { get; set; } = new AnimationsConfig();
        public AppearanceConfig Appearance { get; set; } = new AppearanceConfig();
        public DebugConfig Debug { get; set; } = new DebugConfig();
    }
}