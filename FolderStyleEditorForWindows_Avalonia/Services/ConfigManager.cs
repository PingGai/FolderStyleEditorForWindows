using System;
using System.IO;
using FolderStyleEditorForWindows.Models;
using Tomlyn;

namespace FolderStyleEditorForWindows.Services
{
    public static class ConfigManager
    {
        private const string ConfigFileName = "config.toml";
        private static readonly string _configPath;

        public static AppConfig Config { get; private set; } = new();

        static ConfigManager()
        {
            _configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
            LoadConfig();
        }

        private static void LoadConfig()
        {
            if (!File.Exists(_configPath))
            {
                Config = new AppConfig();
                NormalizeAndPatch(Config);
                SaveConfig();
                return;
            }

            try
            {
                var tomlString = File.ReadAllText(_configPath);
                var modelOptions = new TomlModelOptions
                {
                    IgnoreMissingProperties = true
                };
                Config = Toml.ToModel<AppConfig>(tomlString, null, modelOptions) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config.toml: {ex.Message}");
                Config = new AppConfig();
            }

            NormalizeAndPatch(Config);
            SaveConfig();
        }

        public static void SaveConfig()
        {
            try
            {
                var tomlString = Toml.FromModel(Config);
                File.WriteAllText(_configPath, tomlString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config.toml: {ex.Message}");
            }
        }

        private static void NormalizeAndPatch(AppConfig cfg)
        {
            static string Fix(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return string.Empty;
                }

                return value.Replace("FolderStyleEditorForWindows_Avalonia", "FolderStyleEditorForWindows");
            }

            cfg.Debug ??= new DebugConfig();
            cfg.Debug.EnableOverlay = false;
            cfg.Debug.SvgTestPath = string.IsNullOrWhiteSpace(cfg.Debug.SvgTestPath)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/book-image.svg"
                : Fix(cfg.Debug.SvgTestPath);
            cfg.Debug.PngTestPath = string.IsNullOrWhiteSpace(cfg.Debug.PngTestPath)
                ? "avares://FolderStyleEditorForWindows/Resources/PNG/test-pin.png"
                : Fix(cfg.Debug.PngTestPath);
            cfg.Debug.SvgOpacity = 0.4;

            cfg.AppInfo ??= new AppInfoConfig();
            cfg.AppInfo.HelpIcon = string.IsNullOrWhiteSpace(cfg.AppInfo.HelpIcon)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/message-circle-question-mark.svg"
                : Fix(cfg.AppInfo.HelpIcon);

            cfg.HoverIcon ??= new HoverIconConfig();
            cfg.HoverIcon.DefaultIcon = string.IsNullOrWhiteSpace(cfg.HoverIcon.DefaultIcon)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/file.svg"
                : Fix(cfg.HoverIcon.DefaultIcon);
            cfg.HoverIcon.ErrorIcon = string.IsNullOrWhiteSpace(cfg.HoverIcon.ErrorIcon)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/ban.svg"
                : Fix(cfg.HoverIcon.ErrorIcon);
            if (cfg.HoverIcon.MainIcons != null)
            {
                foreach (var rule in cfg.HoverIcon.MainIcons)
                {
                    rule.IconPath = Fix(rule.IconPath);
                }
            }

            if (cfg.HoverIcon.BadgeIcons != null)
            {
                foreach (var rule in cfg.HoverIcon.BadgeIcons)
                {
                    rule.IconPath = Fix(rule.IconPath);
                }
            }

            cfg.PinIcon ??= new PinIconConfig();
            cfg.PinIcon.MainIcon = string.IsNullOrWhiteSpace(cfg.PinIcon.MainIcon)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/pin.svg"
                : Fix(cfg.PinIcon.MainIcon);
            cfg.PinIcon.PinnedIcon = string.IsNullOrWhiteSpace(cfg.PinIcon.PinnedIcon)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/pin.svg"
                : Fix(cfg.PinIcon.PinnedIcon);
            cfg.PinIcon.UnpinnedIcon = string.IsNullOrWhiteSpace(cfg.PinIcon.UnpinnedIcon)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/pin-off.svg"
                : Fix(cfg.PinIcon.UnpinnedIcon);
            cfg.PinIcon.TestPngPath = string.IsNullOrWhiteSpace(cfg.PinIcon.TestPngPath)
                ? "avares://FolderStyleEditorForWindows/Resources/PNG/test-pin.png"
                : Fix(cfg.PinIcon.TestPngPath);
            if (cfg.PinIcon.BadgeIcons != null)
            {
                foreach (var rule in cfg.PinIcon.BadgeIcons)
                {
                    rule.IconPath = Fix(rule.IconPath);
                }
            }
        }
    }
}
