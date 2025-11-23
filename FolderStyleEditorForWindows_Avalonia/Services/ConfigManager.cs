using System;
using System.IO;
using System.Linq;
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
            // 如果文件不存在，则创建一个具有默认值的实例并保存（带标准化）
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
                // 设置选项以忽略 TOML 文件中存在但在模型中不存在的属性
                var modelOptions = new TomlModelOptions
                {
                    IgnoreMissingProperties = true
                };
                Config = Toml.ToModel<AppConfig>(tomlString, null, modelOptions) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config.toml: {ex.Message}");
                // 加载失败时使用默认配置
                Config = new AppConfig();
            }

            // 统一修正：路径、默认值与 Debug 开关
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

        /// <summary>
        /// 规范化配置：修正 avares 路径、补齐默认值、关闭 Debug 覆盖层，并降低 SVG 不透明度。
        /// 同时将旧的 “FolderStyleEditorForWindows_Avalonia” 前缀统一迁移为 “FolderStyleEditorForWindows”。
        /// </summary>
        private static void NormalizeAndPatch(AppConfig cfg)
        {
            static string Fix(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                return s.Replace("FolderStyleEditorForWindows_Avalonia", "FolderStyleEditorForWindows");
            }

            // 关闭 Debug 覆盖层并统一 SVG 透明度
            cfg.Debug ??= new DebugConfig();
            cfg.Debug.EnableOverlay = false;
            cfg.Debug.SvgTestPath = string.IsNullOrWhiteSpace(cfg.Debug.SvgTestPath)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/book-image.svg"
                : Fix(cfg.Debug.SvgTestPath);
            cfg.Debug.PngTestPath = string.IsNullOrWhiteSpace(cfg.Debug.PngTestPath)
                ? "avares://FolderStyleEditorForWindows/Resources/PNG/test-pin.png"
                : Fix(cfg.Debug.PngTestPath);
            cfg.Debug.SvgOpacity = 0.4;

            // HoverIcon 默认与迁移
            cfg.HoverIcon ??= new HoverIconConfig();
            cfg.HoverIcon.DefaultIcon = string.IsNullOrWhiteSpace(cfg.HoverIcon.DefaultIcon)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/file.svg"
                : Fix(cfg.HoverIcon.DefaultIcon);
            cfg.HoverIcon.ErrorIcon = string.IsNullOrWhiteSpace(cfg.HoverIcon.ErrorIcon)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/ban.svg"
                : Fix(cfg.HoverIcon.ErrorIcon);
            if (cfg.HoverIcon.MainIcons != null)
            {
                foreach (var r in cfg.HoverIcon.MainIcons)
                    r.IconPath = Fix(r.IconPath);
            }
            if (cfg.HoverIcon.BadgeIcons != null)
            {
                foreach (var r in cfg.HoverIcon.BadgeIcons)
                    r.IconPath = Fix(r.IconPath);
            }

            // PinIcon 默认与迁移
            cfg.PinIcon ??= new PinIconConfig();
            cfg.PinIcon.MainIcon = string.IsNullOrWhiteSpace(cfg.PinIcon.MainIcon)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/pin.svg"
                : Fix(cfg.PinIcon.MainIcon);
            cfg.PinIcon.TestPngPath = string.IsNullOrWhiteSpace(cfg.PinIcon.TestPngPath)
                ? "avares://FolderStyleEditorForWindows/Resources/PNG/test-pin.png"
                : Fix(cfg.PinIcon.TestPngPath);
            if (cfg.PinIcon.BadgeIcons != null)
            {
                foreach (var r in cfg.PinIcon.BadgeIcons)
                    r.IconPath = Fix(r.IconPath);
            }
        }
    }
}