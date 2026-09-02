using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using RoosterAudioSwitcher.Models;

namespace RoosterAudioSwitcher.Managers
{
    /// <summary>
    /// Manages loading and saving application settings to a JSON configuration file.
    /// Config files are signed with HMAC-SHA256 to detect corruption of the saved settings.
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
        /// Verifies HMAC-SHA256 integrity of the config file.
        /// </summary>
        private Settings LoadSettings()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    string rawContent = File.ReadAllText(_configPath);

                    // Extract and verify HMAC signature (stored as last line: #HMAC:<hex>)
                    string jsonContent;
                    if (!TryExtractAndVerifyHmac(rawContent, out jsonContent))
                    {
                        Logger.LogWarning("Config file integrity check failed. Using default settings.");
                        return new Settings();
                    }

                    var settings = JsonSerializer.Deserialize<Settings>(jsonContent) ?? new Settings();

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

                    if (string.IsNullOrWhiteSpace(settings.ThirdDeviceHotKey))
                    {
                        settings.ThirdDeviceHotKey = "Ctrl+Alt+F";
                    }

                    return settings;
                }
                catch (Exception ex)
                {
                    // If JSON is corrupted, log and use defaults
                    Logger.LogError($"Error loading config: {ex.Message}. Using default settings.");
                    return new Settings();
                }
            }

            // Return new default settings if file doesn't exist yet
            return new Settings();
        }

        /// <summary>
        /// Extracts the HMAC signature from the raw config content and verifies it.
        /// On success, jsonContent holds the exact JSON string that was signed.
        /// </summary>
        private static bool TryExtractAndVerifyHmac(string rawContent, out string jsonContent)
        {
            // File layout written by SaveSettings:
            //   <json><newline>#HMAC:<hex><newline>
            // Slice at the signature marker and take everything before it as the signed
            // content. Slicing (rather than rejoining lines) guarantees the bytes we
            // verify are byte-identical to the bytes we signed.
            const string hmacMarker = "\n#HMAC:";
            int markerIndex = rawContent.LastIndexOf(hmacMarker, StringComparison.Ordinal);

            if (markerIndex < 0)
            {
                // No HMAC signature present — treat as unsigned (legacy or first-run).
                // Still try to parse the JSON.
                jsonContent = rawContent;
                return true;
            }

            jsonContent = rawContent.Substring(0, markerIndex).TrimEnd();
            string storedHmac = rawContent.Substring(markerIndex + hmacMarker.Length).Trim();

            // A SHA256 HMAC is exactly 64 hex characters.
            if (storedHmac.Length != 64)
                return false;

            // Recompute HMAC over the JSON content and compare
            try
            {
                byte[] key = GetHmacKey();
                string computedHex;

                using (var hmac = new HMACSHA256(key))
                {
                    computedHex = Convert.ToHexString(
                        hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(jsonContent)))
                        .ToLowerInvariant();
                }

                // Constant-time comparison to prevent timing attacks
                return SecureEquals(storedHmac, computedHex);
            }
            catch
            {
                // If HMAC computation fails, reject the file and fall back to defaults.
                return false;
            }
        }

        /// <summary>
        /// Derives the HMAC key from a fixed application seed.
        ///
        /// The key MUST be deterministic: the config is signed on save and verified on the
        /// next launch, which runs in a different process. DPAPI (ProtectedData.Protect)
        /// cannot be used for this — its ciphertext is randomized on every call (fresh IV),
        /// so a DPAPI-derived key differs on every launch and every saved config would fail
        /// verification, silently resetting the user's settings to defaults on every restart.
        ///
        /// Because the seed ships inside the application, this HMAC is an integrity
        /// (corruption) check, not a tamper- or user-bound secret. That is appropriate for
        /// this config, which stores no sensitive data.
        /// </summary>
        private static byte[] GetHmacKey()
        {
            const string seed = "RoosterAudioSwitcher-ConfigIntegritySeed";
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(seed));
        }

        /// <summary>
        /// Constant-time string comparison to prevent timing attacks.
        /// </summary>
        private static bool SecureEquals(string a, string b)
        {
            if (a.Length != b.Length)
                return false;

            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }

            return result == 0;
        }

        /// <summary>
        /// Saves the current settings to the JSON config file with an HMAC-SHA256 signature.
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

                // Compute HMAC signature over the exact JSON content
                byte[] key = GetHmacKey();
                string hmacHex;
                using (var hmac = new HMACSHA256(key))
                {
                    hmacHex = Convert.ToHexString(
                        hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json)))
                        .ToLowerInvariant();
                }

                // Write JSON followed by HMAC signature as a comment line.
                // Lines starting with '#' are ignored during parsing.
                string signedContent = json + Environment.NewLine + $"#HMAC:{hmacHex}" + Environment.NewLine;
                File.WriteAllText(_configPath, signedContent);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error saving config: {ex.Message}");
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
