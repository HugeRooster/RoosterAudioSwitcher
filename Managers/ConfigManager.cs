using System;
using System.IO;
using System.Text.Json;
using RoosterAudioSwitcher.Models;

namespace RoosterAudioSwitcher.Managers
{
    /// <summary>
    /// Manages loading and saving application settings to a JSON configuration file.
    /// </summary>
    public class ConfigManager
    {
        private readonly string _configPath;
        private Settings _settings;

        /// <summary>
        /// Initializes a new instance of the ConfigManager.
        /// </summary>
        public ConfigManager()
        {
            // Use AppData folder for config file location
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appConfigDir = Path.Combine(appDataPath, "RoosterAudioSwitcher");
            _configPath = Path.Combine(appConfigDir, "config.json");

            // Create directory if it doesn't exist
            if (!Directory.Exists(appConfigDir))
            {
                Directory.CreateDirectory(appConfigDir);
            }

            // Load existing config or create default
            _settings = LoadSettings();
        }

        /// <summary>
        /// Gets the current settings.
        /// </summary>
        public Settings Settings => _settings;

        /// <summary>
        /// Loads settings from the JSON config file, or creates a new default Settings object if the file doesn't exist.
        /// </summary>
        private Settings LoadSettings()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    string json = File.ReadAllText(_configPath);
                    var settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();

                    // Backward compatibility migration from legacy single-hotkey setting.
                    if (string.IsNullOrWhiteSpace(settings.SwitchToDeviceHotKey))
                    {
                        settings.SwitchToDeviceHotKey = string.IsNullOrWhiteSpace(settings.HotKey)
                            ? "Ctrl+Alt+S"
                            : settings.HotKey;
                    }

                    if (string.IsNullOrWhiteSpace(settings.ReturnToDefaultHotKey))
                    {
                        settings.ReturnToDefaultHotKey = "Ctrl+Alt+D";
                    }

                    return settings;
                }
                catch (Exception ex)
                {
                    // If JSON is corrupted, log and use defaults
                    Console.WriteLine($"Error loading config: {ex.Message}. Using default settings.");
                    return new Settings();
                }
            }

            // Return new default settings if file doesn't exist yet
            return new Settings();
        }

        /// <summary>
        /// Saves the current settings to the JSON config file.
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true // Pretty-print JSON for readability
                };
                string json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
            }
        }

        /// <summary>
        /// Resets settings to defaults and saves.
        /// </summary>
        public void ResetToDefaults()
        {
            _settings = new Settings();
            SaveSettings();
        }

        /// <summary>
        /// Gets the config file path (for testing/debugging).
        /// </summary>
        public string ConfigPath => _configPath;
    }
}
