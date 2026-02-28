using System;
using System.IO;
using Newtonsoft.Json;

namespace FamilyTreeApp.Core
{
    /// <summary>
    /// Manages saving and loading of application settings.
    /// </summary>
    public static class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FamilyTreeApp",
            "settings.json"
        );
        
        private static AppSettings? _currentSettings;
        
        /// <summary>
        /// Gets the current application settings.
        /// </summary>
        public static AppSettings Current
        {
            get
            {
                if (_currentSettings == null)
                {
                    _currentSettings = Load();
                }
                return _currentSettings;
            }
        }
        
        /// <summary>
        /// Loads settings from disk, or returns defaults if not found.
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (settings != null)
                    {
                        _currentSettings = settings;
                        return settings;
                    }
                }
            }
            catch (Exception)
            {
                // If loading fails, return defaults
            }
            
            _currentSettings = AppSettings.GetDefaults();
            return _currentSettings;
        }
        
        /// <summary>
        /// Saves the current settings to disk.
        /// </summary>
        public static void Save()
        {
            if (_currentSettings == null)
            {
                return;
            }
            
            Save(_currentSettings);
        }
        
        /// <summary>
        /// Saves the given settings to disk.
        /// </summary>
        public static void Save(AppSettings settings)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
                _currentSettings = settings;
            }
            catch (Exception)
            {
                // Handle save failure silently or log
            }
        }
        
        /// <summary>
        /// Resets settings to defaults and saves.
        /// </summary>
        public static void ResetToDefaults()
        {
            _currentSettings = AppSettings.GetDefaults();
            Save(_currentSettings);
        }
    }
}
