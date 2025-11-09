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
            // 如果文件不存在，则创建一个具有默认值的实例并保存
            if (!File.Exists(_configPath))
            {
                Config = new AppConfig();
                SaveConfig();
            }
            else
            {
                try
                {
                    var tomlString = File.ReadAllText(_configPath);
                    // 设置选项以忽略 TOML 文件中存在但在模型中不存在的属性
                    var modelOptions = new TomlModelOptions
                    {
                        IgnoreMissingProperties = true
                    };
                    Config = Toml.ToModel<AppConfig>(tomlString, null, modelOptions);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading config.toml: {ex.Message}");
                    // 加载失败时使用默认配置
                    Config = new AppConfig();
                }
            }
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
    }
}