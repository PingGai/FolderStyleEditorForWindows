using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using FolderStyleEditorForWindows.Models;
using YamlDotNet.Serialization;

namespace FolderStyleEditorForWindows.Services
{
    public class LocalizationManager : INotifyPropertyChanged
    {
        // Singleton instance
        private static readonly Lazy<LocalizationManager> _lazyInstance = new(() => new LocalizationManager());
        public static LocalizationManager Instance => _lazyInstance.Value;

        private Dictionary<string, string> _strings = new();
        private string _currentCultureName = "en-US";
        private readonly LanguageDefaultsConfig _languageDefaults;

        public ObservableCollection<LanguageInfo> AvailableLanguages { get; } = new();

        private LocalizationManager()
        {
            _languageDefaults = ConfigManager.Config.LanguageDefaults ?? new LanguageDefaultsConfig();
            LoadAvailableLanguages();
            
            var config = ConfigManager.Config;
            string languageToLoad;

            if (config.LanguageConfigured && !string.IsNullOrWhiteSpace(config.Language))
            {
                languageToLoad = config.Language;
            }
            else
            {
                languageToLoad = DetermineInitialLanguage(config);
            }

            SwitchLanguage(languageToLoad);
        }

        private void LoadAvailableLanguages()
        {
            AvailableLanguages.Clear();
            var assembly = Assembly.GetExecutingAssembly();
            var deserializer = new DeserializerBuilder().Build();
            const string prefix = "FolderStyleEditorForWindows.Lang.";
            const string suffix = ".yml";

            var langResources = assembly.GetManifestResourceNames()
                .Where(r => r.StartsWith(prefix) && r.EndsWith(suffix));

            foreach (var resourceName in langResources)
            {
                try
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream == null) continue;
                    
                    using var reader = new StreamReader(stream);
                    var yamlContent = reader.ReadToEnd();
                    var langDict = deserializer.Deserialize<Dictionary<string, string>>(yamlContent);

                    if (langDict != null && langDict.TryGetValue("LanguageName", out var langName))
                    {
                        var culture = resourceName.Substring(prefix.Length, resourceName.Length - prefix.Length - suffix.Length);
                        AvailableLanguages.Add(new LanguageInfo { Name = langName, Culture = culture });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error discovering language from resource {resourceName}: {ex.Message}");
                }
            }
        }

        public void SwitchLanguage(string cultureName)
        {
            try
            {
                // Find if the requested culture is available
                var langInfo = AvailableLanguages.FirstOrDefault(l => l.Culture.Equals(cultureName, StringComparison.OrdinalIgnoreCase));
                
                // Fallback to English or the first available language if the requested one is not found
                if (langInfo == null)
                {
                    langInfo = AvailableLanguages.FirstOrDefault(l => l.Culture.Equals("en-US", StringComparison.OrdinalIgnoreCase))
                                 ?? AvailableLanguages.FirstOrDefault();
                }

                if (langInfo == null) // No languages found at all
                {
                    _strings = new Dictionary<string, string>();
                    Invalidate();
                    return;
                }

                // If the language is already loaded, do nothing
                if (_currentCultureName == langInfo.Culture && _strings.Any())
                {
                    return;
                }
                
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = $"FolderStyleEditorForWindows.Lang.{langInfo.Culture}.yml";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    _strings = new Dictionary<string, string>();
                    Invalidate();
                    return;
                }

                using var reader = new StreamReader(stream);
                var yamlContent = reader.ReadToEnd();

                var deserializer = new DeserializerBuilder().Build();

                _strings = deserializer.Deserialize<Dictionary<string, string>>(yamlContent)
                           ?? new Dictionary<string, string>();
                
                _currentCultureName = langInfo.Culture;
                
                // Save the selected language to config
                ConfigManager.Config.Language = _currentCultureName;
                ConfigManager.Config.LanguageConfigured = true;
                ConfigManager.SaveConfig();

                // Notify UI to refresh all bindings
                Invalidate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading language file for culture {cultureName}: {ex.Message}");
                _strings = new Dictionary<string, string>();
                Invalidate();
            }
        }

        private string DetermineInitialLanguage(AppConfig config)
        {
            if (!string.IsNullOrWhiteSpace(config.Language) &&
                !string.Equals(config.Language, "en-US", StringComparison.OrdinalIgnoreCase))
            {
                config.LanguageConfigured = true;
                return config.Language;
            }

            return ResolveLanguageFromSystemCulture();
        }

        private string ResolveLanguageFromSystemCulture()
        {
            var candidates = BuildCultureCandidates(CultureInfo.CurrentUICulture);

            if (IsCultureMatch(candidates, _languageDefaults.ChineseCultures))
            {
                return PickAvailableCulture(_languageDefaults.ChineseCultures, "zh-CN");
            }

            if (IsCultureMatch(candidates, _languageDefaults.EnglishCultures))
            {
                return PickAvailableCulture(_languageDefaults.EnglishCultures, "en-US");
            }

            if (!string.IsNullOrWhiteSpace(_languageDefaults.DefaultCulture))
            {
                return PickAvailableCulture(new[] { _languageDefaults.DefaultCulture }, _languageDefaults.DefaultCulture);
            }

            return AvailableLanguages.FirstOrDefault()?.Culture ?? "en-US";
        }

        private static HashSet<string> BuildCultureCandidates(CultureInfo culture)
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddCandidate(string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    candidates.Add(value);
                }
            }

            AddCandidate(culture.Name);
            AddCandidate(culture.Parent?.Name);
            AddCandidate(culture.TwoLetterISOLanguageName);

            return candidates;
        }

        private static bool IsCultureMatch(HashSet<string> candidates, string[]? configuredCultures)
        {
            if (configuredCultures == null || configuredCultures.Length == 0)
            {
                return false;
            }

            foreach (var configured in configuredCultures)
            {
                if (string.IsNullOrWhiteSpace(configured))
                {
                    continue;
                }

                if (candidates.Contains(configured))
                {
                    return true;
                }

                var shortCode = configured.Split('-')[0];
                if (candidates.Contains(shortCode))
                {
                    return true;
                }
            }

            return false;
        }

        private string PickAvailableCulture(IEnumerable<string>? preferredCultures, string fallback)
        {
            if (preferredCultures != null)
            {
                foreach (var culture in preferredCultures)
                {
                    if (string.IsNullOrWhiteSpace(culture))
                    {
                        continue;
                    }

                    if (IsLanguageAvailable(culture))
                    {
                        return culture;
                    }
                }
            }

            if (IsLanguageAvailable(fallback))
            {
                return fallback;
            }

            return AvailableLanguages.FirstOrDefault()?.Culture ?? fallback;
        }

        private bool IsLanguageAvailable(string culture) =>
            AvailableLanguages.Any(l => l.Culture.Equals(culture, StringComparison.OrdinalIgnoreCase));

        public string GetCurrentCulture() => _currentCultureName;

        public string this[string key]
        {
            get
            {
                if (_strings.TryGetValue(key, out var value))
                {
                    return value;
                }
                return $"[{key}]";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Invalidate()
        {
            OnPropertyChanged(string.Empty);
        }
    }
}