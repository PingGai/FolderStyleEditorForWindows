namespace FolderStyleEditorForWindows.Models
{
    public class AppConfig
    {
        public int ConfigSchemaVersion { get; set; } = 2;
        public string? Language { get; set; }
        public bool LanguageConfigured { get; set; }
        public AppFeaturesConfig Features { get; set; } = new AppFeaturesConfig();
        public AppInfoConfig AppInfo { get; set; } = new AppInfoConfig();
        public HoverIconConfig HoverIcon { get; set; } = new HoverIconConfig();
        public PinIconConfig PinIcon { get; set; } = new PinIconConfig();
        public AnimationsConfig Animations { get; set; } = new AnimationsConfig();
        public AppearanceConfig Appearance { get; set; } = new AppearanceConfig();
        public DebugConfig Debug { get; set; } = new DebugConfig();
        public LanguageDefaultsConfig LanguageDefaults { get; set; } = new LanguageDefaultsConfig();
        public DragOverlayConfig DragOverlay { get; set; } = new DragOverlayConfig();
        public DialogBehaviorConfig Dialog { get; set; } = new DialogBehaviorConfig();
        public UiBehaviorConfig Ui { get; set; } = new UiBehaviorConfig();
        public AppPathConfig Paths { get; set; } = new AppPathConfig();
        public PermissionBehaviorConfig Permissions { get; set; } = new PermissionBehaviorConfig();
        public FrameRateBehaviorConfig FrameRate { get; set; } = new FrameRateBehaviorConfig();
    }

    public class LanguageDefaultsConfig
    {
        public string[] ChineseCultures { get; set; } = new[] { "zh-CN", "zh-Hans", "zh-Hant", "zh-TW", "zh-HK" };
        public string[] EnglishCultures { get; set; } = new[] { "en-US", "en-GB", "en" };
        public string DefaultCulture { get; set; } = "zh-CN";
    }

    public class DragOverlayConfig
    {
        public bool DismissOnEsc { get; set; } = false;
        public bool ShowPrimaryButton { get; set; } = false;
        public bool ShowSecondaryButton { get; set; } = false;
        public bool AllowOverlayClickDismiss { get; set; } = false;
    }

    public class DialogBehaviorConfig
    {
        public bool DefaultDismissOnEsc { get; set; } = true;
        public bool DefaultAllowOverlayClickDismiss { get; set; } = false;
    }

    public class UiBehaviorConfig
    {
        public string DragIndicatorStrokeColor { get; set; } = "#AAB7C3";
        public double DragIndicatorStrokeOpacity { get; set; } = 0.86;
        public string DragOverlayMainTextColor { get; set; } = "#303034";
        public string DragOverlayWarningTextColor { get; set; } = "#E07167";
    }

    public class AppPathConfig
    {
        public string? PreferredDataRoot { get; set; }
    }

    public class PermissionBehaviorConfig
    {
        public bool SuppressElevationPrompt { get; set; } = false;
    }

    public class FrameRateBehaviorConfig
    {
        public int StaticContentRefreshFps { get; set; } = 0;
        public int BackgroundAmbientFps { get; set; } = 8;
        public int HomeTitleAmbientFps { get; set; } = 15;
        public int AdminTitleAmbientFps { get; set; } = 15;
        public int ActiveInteractionFps { get; set; } = 60;
        public bool UseDisplayRefreshRateAsMaxFps { get; set; } = true;
        public int ManualMaxFps { get; set; } = 120;
        public int HoverCooldownMs { get; set; } = 120;
        public int ScrollCooldownMs { get; set; } = 240;
        public int DragCooldownMs { get; set; } = 280;
        public bool ShowPerformanceMonitor { get; set; } = false;
        public bool ShowDetailedPerformanceMonitor { get; set; } = false;
        public bool ShowComponentFpsBadges { get; set; } = false;
        public bool EnableComponentExcludeMode { get; set; } = false;
        public bool ExcludePinGlow { get; set; } = false;
        public bool ExcludeBottomActionButtons { get; set; } = false;
        public bool ExcludeActualTopmost { get; set; } = false;
        public bool DisableEditScrollAnimations { get; set; } = false;
        public bool ShowFrameRateOverlay { get; set; } = false;
        public bool ShowDetailedFrameRateOverlay { get; set; } = false;
    }
}
