namespace FolderStyleEditorForWindows.Models
{
    public class AppConfig
    {
        public string? Language { get; set; }
        public bool LanguageConfigured { get; set; }
        public AppFeaturesConfig Features { get; set; } = new AppFeaturesConfig();
        public HoverIconConfig HoverIcon { get; set; } = new HoverIconConfig();
        public PinIconConfig PinIcon { get; set; } = new PinIconConfig();
        public AnimationsConfig Animations { get; set; } = new AnimationsConfig();
        public AppearanceConfig Appearance { get; set; } = new AppearanceConfig();
        public DebugConfig Debug { get; set; } = new DebugConfig();
        public LanguageDefaultsConfig LanguageDefaults { get; set; } = new LanguageDefaultsConfig();
    }

    public class LanguageDefaultsConfig
    {
        public string[] ChineseCultures { get; set; } = new[] { "zh-CN", "zh-Hans", "zh-Hant", "zh-TW", "zh-HK" };
        public string[] EnglishCultures { get; set; } = new[] { "en-US", "en-GB", "en" };
        public string DefaultCulture { get; set; } = "zh-CN";
    }
}