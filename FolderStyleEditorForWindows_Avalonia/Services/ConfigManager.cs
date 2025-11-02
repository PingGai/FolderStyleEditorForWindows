using System;
using System.IO;
using FolderStyleEditorForWindows.Models;
using Newtonsoft.Json;
using Tomlyn;

namespace FolderStyleEditorForWindows.Services
{
    public static class ConfigManager
    {
        private static readonly string _appDataDirectory;
        private const string AppConfigFileName = "settings.json";
        private const string FeaturesConfigFileName = "config.toml";

        public static AppFeaturesConfig Features { get; private set; } = new();

        static ConfigManager()
        {
            string tempPath = Path.GetTempPath();
            _appDataDirectory = Path.Combine(tempPath, "FolderStyleEditorForWindows");

            if (!Directory.Exists(_appDataDirectory))
            {
                Directory.CreateDirectory(_appDataDirectory);
            }
            
            LoadFeaturesConfig();
        }

        public static string AppDataDirectory => _appDataDirectory;

        public static string GetConfigFilePath(string fileName)
        {
            return Path.Combine(_appDataDirectory, fileName);
        }

        public static AppConfig LoadAppConfig()
        {
            var configFilePath = GetConfigFilePath(AppConfigFileName);
            if (File.Exists(configFilePath))
            {
                try
                {
                    var json = File.ReadAllText(configFilePath);
                    return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading app config: {ex.Message}");
                }
            }
            return new AppConfig();
        }

        public static void SaveAppConfig(AppConfig config)
        {
            var configFilePath = GetConfigFilePath(AppConfigFileName);
            try
            {
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving app config: {ex.Message}");
            }
        }
        
        private static void LoadFeaturesConfig()
        {
            var configFilePath = Path.Combine(AppContext.BaseDirectory, FeaturesConfigFileName);
            if (File.Exists(configFilePath))
            {
                try
                {
                    var content = File.ReadAllText(configFilePath);
                    Features = Tomlyn.Toml.ToModel<AppFeaturesConfig>(content);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading features config: {ex.Message}");
                    Features = new AppFeaturesConfig();
                }
            }
            else
            {
                Console.WriteLine($"Features config file not found: {configFilePath}");
                Features = new AppFeaturesConfig();
            }
        }
    }
}