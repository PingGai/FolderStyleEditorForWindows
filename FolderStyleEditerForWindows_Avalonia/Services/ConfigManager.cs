using System;
using System.IO;
using FolderStyleEditerForWindows.Models;
using Newtonsoft.Json;

namespace FolderStyleEditerForWindows.Services
{
    public static class ConfigManager
    {
        private static readonly string _appDataDirectory;
        private const string AppConfigFileName = "settings.json";

        static ConfigManager()
        {
            string tempPath = Path.GetTempPath();
            _appDataDirectory = Path.Combine(tempPath, "FolderStyleEditerForWindows");

            if (!Directory.Exists(_appDataDirectory))
            {
                Directory.CreateDirectory(_appDataDirectory);
            }
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
    }
}