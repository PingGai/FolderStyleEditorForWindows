using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FolderStyleEditorForWindows.Models;
using Tomlyn;

namespace FolderStyleEditorForWindows.Services
{
    public sealed class ConfigMigrationResult
    {
        public string? SourcePath { get; init; }
        public bool Migrated { get; init; }
        public bool WroteBack { get; init; }
    }

    public static class ConfigManager
    {
        private const string ConfigFileName = "config.toml";
        private static readonly object SyncRoot = new();
        private static readonly string _configPath;

        public static string AppDataDirectory { get; }
        public static string ConfigPath => _configPath;
        public static ConfigMigrationResult LastMigrationResult { get; private set; } = new();

        public static AppConfig Config { get; private set; } = new();

        static ConfigManager()
        {
            AppDataDirectory = ResolveAppDataDirectory();
            Directory.CreateDirectory(AppDataDirectory);
            _configPath = Path.Combine(AppDataDirectory, ConfigFileName);
            LoadConfig();
        }

        private static string ResolveAppDataDirectory()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(local))
            {
                return Path.Combine(Path.GetTempPath(), "FolderStyleEditorForWindows");
            }

            return Path.Combine(local, "FolderStyleEditorForWindows");
        }

        private static void LoadConfig()
        {
            string? sourcePath = null;
            string? sourceContent = null;
            var migrated = false;

            if (File.Exists(_configPath))
            {
                sourcePath = _configPath;
                sourceContent = SafeReadUtf8(sourcePath);
            }
            else
            {
                sourcePath = FindLatestLegacyConfigPath();
                if (!string.IsNullOrWhiteSpace(sourcePath))
                {
                    sourceContent = SafeReadUtf8(sourcePath);
                    migrated = true;
                }
            }

            Config = ParseConfig(sourceContent);
            NormalizeAndPatch(Config);

            SaveConfigInternal(sourceContent);

            LastMigrationResult = new ConfigMigrationResult
            {
                SourcePath = sourcePath,
                Migrated = migrated,
                WroteBack = true
            };
        }

        public static void SaveConfig()
        {
            lock (SyncRoot)
            {
                string? baseline = null;
                if (File.Exists(_configPath))
                {
                    baseline = SafeReadUtf8(_configPath);
                }

                SaveConfigInternal(baseline);
            }
        }

        private static void SaveConfigInternal(string? baseline)
        {
            try
            {
                var text = RenderConfigDocument(Config, baseline);
                File.WriteAllText(_configPath, text, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config.toml: {ex.Message}");
            }
        }

        private static AppConfig ParseConfig(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new AppConfig();
            }

            try
            {
                var modelOptions = new TomlModelOptions
                {
                    IgnoreMissingProperties = true
                };
                return Toml.ToModel<AppConfig>(content, null, modelOptions) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config.toml: {ex.Message}");
                return new AppConfig();
            }
        }

        private static string? FindLatestLegacyConfigPath()
        {
            var candidates = new[]
            {
                Path.Combine(Path.GetTempPath(), "WindowsFolderStyleEditor", ConfigFileName),
                Path.Combine(Path.GetTempPath(), "FolderStyleEditorForWindows", ConfigFileName),
                Path.Combine(AppContext.BaseDirectory, ConfigFileName)
            };

            return candidates
                .Where(File.Exists)
                .OrderByDescending(path => new FileInfo(path).LastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static string? SafeReadUtf8(string path)
        {
            try
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch
            {
                try
                {
                    return File.ReadAllText(path);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static string RenderConfigDocument(AppConfig cfg, string? baseline)
        {
            var text = string.IsNullOrWhiteSpace(baseline)
                ? DefaultConfigTemplate()
                : baseline!;

            text = UpsertRootValue(text, "ConfigSchemaVersion", cfg.ConfigSchemaVersion.ToString());
            text = UpsertRootValue(text, "Language", QuoteString(cfg.Language ?? string.Empty));
            text = UpsertRootValue(text, "LanguageConfigured", cfg.LanguageConfigured ? "true" : "false");

            text = UpsertSectionValue(text, "Features", "PinDoubleClickThreshold", cfg.Features.Features.PinDoubleClickThreshold.ToString());
            text = UpsertSectionValue(text, "Features.PermissionPrompt", "SuppressElevationPrompt", cfg.Features.PermissionPrompt.SuppressElevationPrompt ? "true" : "false");

            text = UpsertSectionValue(text, "Dialog", "DefaultDismissOnEsc", cfg.Dialog.DefaultDismissOnEsc ? "true" : "false");
            text = UpsertSectionValue(text, "Dialog", "DefaultAllowOverlayClickDismiss", cfg.Dialog.DefaultAllowOverlayClickDismiss ? "true" : "false");

            text = UpsertSectionValue(text, "DragOverlay", "DismissOnEsc", cfg.DragOverlay.DismissOnEsc ? "true" : "false");
            text = UpsertSectionValue(text, "DragOverlay", "ShowPrimaryButton", cfg.DragOverlay.ShowPrimaryButton ? "true" : "false");
            text = UpsertSectionValue(text, "DragOverlay", "ShowSecondaryButton", cfg.DragOverlay.ShowSecondaryButton ? "true" : "false");
            text = UpsertSectionValue(text, "DragOverlay", "AllowOverlayClickDismiss", cfg.DragOverlay.AllowOverlayClickDismiss ? "true" : "false");

            text = UpsertSectionValue(text, "Ui", "DragIndicatorStrokeColor", QuoteString(cfg.Ui.DragIndicatorStrokeColor));
            text = UpsertSectionValue(text, "Ui", "DragIndicatorStrokeOpacity", cfg.Ui.DragIndicatorStrokeOpacity.ToString("0.###"));
            text = UpsertSectionValue(text, "Ui", "DragOverlayMainTextColor", QuoteString(cfg.Ui.DragOverlayMainTextColor));
            text = UpsertSectionValue(text, "Ui", "DragOverlayWarningTextColor", QuoteString(cfg.Ui.DragOverlayWarningTextColor));

            text = UpsertSectionValue(text, "Paths", "PreferredDataRoot", QuoteString(cfg.Paths.PreferredDataRoot ?? AppDataDirectory));

            text = UpsertSectionValue(text, "Permissions", "SuppressElevationPrompt", cfg.Permissions.SuppressElevationPrompt ? "true" : "false");

            text = UpsertSectionValue(text, "Appearance", "SvgDefaultColor", QuoteString(cfg.Appearance.SvgDefaultColor ?? "#ff606064"));

            return text;
        }

        private static string UpsertRootValue(string text, string key, string value)
        {
            var pattern = $"(?m)^\\s*{Regex.Escape(key)}\\s*=.*$";
            var line = $"{key} = {value}";
            if (Regex.IsMatch(text, pattern))
            {
                return Regex.Replace(text, pattern, line);
            }

            return $"{line}{Environment.NewLine}{text}";
        }

        private static string UpsertSectionValue(string text, string section, string key, string value)
        {
            var sectionPattern = $"(?m)^\\[{Regex.Escape(section)}\\]\\s*$";
            var keyPattern = $"(?m)^\\s*{Regex.Escape(key)}\\s*=.*$";
            var line = $"{key} = {value}";

            var sectionMatch = Regex.Match(text, sectionPattern);
            if (!sectionMatch.Success)
            {
                var builder = new StringBuilder(text.TrimEnd());
                builder.AppendLine();
                builder.AppendLine();
                builder.AppendLine($"[{section}]");
                builder.AppendLine(line);
                return builder.ToString() + Environment.NewLine;
            }

            var sectionStart = sectionMatch.Index;
            var sectionRegex = new Regex("(?m)^\\[.+\\]\\s*$");
            var nextSectionMatch = sectionRegex.Match(text, sectionMatch.Index + sectionMatch.Length);
            var sectionEnd = nextSectionMatch.Success ? nextSectionMatch.Index : text.Length;
            var sectionText = text.Substring(sectionStart, sectionEnd - sectionStart);

            if (Regex.IsMatch(sectionText, keyPattern))
            {
                var replaced = Regex.Replace(sectionText, keyPattern, line);
                return text.Remove(sectionStart, sectionEnd - sectionStart).Insert(sectionStart, replaced);
            }

            var insertionPoint = sectionStart + sectionText.Length;
            var prefix = text.Substring(0, insertionPoint).TrimEnd();
            var suffix = text.Substring(insertionPoint);
            return $"{prefix}{Environment.NewLine}{line}{Environment.NewLine}{suffix.TrimStart('\r', '\n')}";
        }

        private static string QuoteString(string value)
        {
            var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"\"{escaped}\"";
        }

        private static string DefaultConfigTemplate() =>
@"# FolderStyleEditorForWindows configuration
ConfigSchemaVersion = 2
Language = """"
LanguageConfigured = false

[Features]
PinDoubleClickThreshold = 500

[Features.PermissionPrompt]
SuppressElevationPrompt = false

[DragOverlay]
DismissOnEsc = false
ShowPrimaryButton = false
ShowSecondaryButton = false
AllowOverlayClickDismiss = false

[Dialog]
DefaultDismissOnEsc = true
DefaultAllowOverlayClickDismiss = false

[Ui]
DragIndicatorStrokeColor = ""#AAB7C3""
DragIndicatorStrokeOpacity = 0.86
DragOverlayMainTextColor = ""#303034""
DragOverlayWarningTextColor = ""#E07167""

[Paths]
PreferredDataRoot = """"

[Permissions]
SuppressElevationPrompt = false
";

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

            cfg.ConfigSchemaVersion = Math.Max(cfg.ConfigSchemaVersion, 2);
            cfg.Debug ??= new DebugConfig();
            cfg.Debug.EnableOverlay = false;
            cfg.Debug.SvgTestPath = string.IsNullOrWhiteSpace(cfg.Debug.SvgTestPath)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/book-image.svg"
                : Fix(cfg.Debug.SvgTestPath);
            cfg.Debug.PngTestPath = string.IsNullOrWhiteSpace(cfg.Debug.PngTestPath)
                ? "avares://FolderStyleEditorForWindows/Resources/PNG/test-pin.png"
                : Fix(cfg.Debug.PngTestPath);
            cfg.Debug.SvgOpacity = Math.Clamp(cfg.Debug.SvgOpacity, 0, 1);

            cfg.AppInfo ??= new AppInfoConfig();
            cfg.AppInfo.HelpIcon = string.IsNullOrWhiteSpace(cfg.AppInfo.HelpIcon)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/message-circle-question-mark.svg"
                : Fix(cfg.AppInfo.HelpIcon);
            cfg.AppInfo.GitHubIcon = string.IsNullOrWhiteSpace(cfg.AppInfo.GitHubIcon)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/github.svg"
                : Fix(cfg.AppInfo.GitHubIcon);
            cfg.AppInfo.StarIcon = string.IsNullOrWhiteSpace(cfg.AppInfo.StarIcon)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/star.svg"
                : Fix(cfg.AppInfo.StarIcon);

            cfg.HoverIcon ??= new HoverIconConfig();
            cfg.HoverIcon.DefaultIcon = string.IsNullOrWhiteSpace(cfg.HoverIcon.DefaultIcon)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/file.svg"
                : Fix(cfg.HoverIcon.DefaultIcon);
            cfg.HoverIcon.ErrorIcon = string.IsNullOrWhiteSpace(cfg.HoverIcon.ErrorIcon)
                ? "avares://FolderStyleEditorForWindows/Resources/SVG/ban.svg"
                : Fix(cfg.HoverIcon.ErrorIcon);

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

            cfg.Features ??= new AppFeaturesConfig();
            cfg.Features.PermissionPrompt ??= new PermissionPromptConfig();
            cfg.DragOverlay ??= new DragOverlayConfig();
            cfg.Dialog ??= new DialogBehaviorConfig();
            cfg.Ui ??= new UiBehaviorConfig();
            cfg.Paths ??= new AppPathConfig();
            cfg.Paths.PreferredDataRoot ??= AppDataDirectory;
            cfg.Permissions ??= new PermissionBehaviorConfig();
            cfg.Features.PermissionPrompt.SuppressElevationPrompt =
                cfg.Features.PermissionPrompt.SuppressElevationPrompt || cfg.Permissions.SuppressElevationPrompt;
            cfg.Permissions.SuppressElevationPrompt = cfg.Features.PermissionPrompt.SuppressElevationPrompt;
        }
    }
}
