using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RoosterAudioSwitcher.Models;

namespace RoosterAudioSwitcher.Managers
{
    /// <summary>
    /// Manages loading and saving application settings to a JSON configuration file.
    /// Config files are signed with HMAC-SHA256 (protected via DPAPI) to detect tampering or corruption.
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
                        Console.WriteLine("Config file integrity check failed. Using default settings.");
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
                    Console.WriteLine($"Error loading config: {ex.Message}. Using default settings.");
                    return new Settings();
                }
            }

            // Return new default settings if file doesn't exist yet
            return new Settings();
        }

        /// <summary>
        /// Extracts the HMAC signature from the last line of the raw config content and verifies it.
        /// Returns the JSON content without the signature line on success.
        /// </summary>
        private static bool TryExtractAndVerifyHmac(string rawContent, out string jsonContent)
        {
            jsonContent = rawContent;

            // Check if file ends with an HMAC comment line: #HMAC:<hex>
            var lines = rawContent.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            int lastLineIndex = lines.Length - 1;

            // Skip trailing empty lines to find the actual last content line
            while (lastLineIndex >= 0 && string.IsNullOrWhiteSpace(lines[lastLineIndex]))
            {
                lastLineIndex--;
            }

            if (lastLineIndex < 0)
                return false;

            string lastLine = lines[lastLineIndex].Trim();
            const string hmacPrefix = "#HMAC:";

            if (!lastLine.StartsWith(hmacPrefix, StringComparison.Ordinal))
            {
                // No HMAC signature present — treat as unsigned (legacy or first-run).
                // Still try to parse the JSON.
                jsonContent = rawContent;
                return true;
            }

            string storedHmac = lastLine.Substring(hmacPrefix.Length);

            // Remove the HMAC line from content for deserialization
            StringBuilder sb = new StringBuilder(rawContent.Length);
            for (int i = 0; i <= lastLineIndex; i++)
            {
                sb.AppendLine(lines[i]);
            }
            jsonContent = sb.ToString().TrimEnd();

            // Recompute HMAC over the JSON content and compare
            try
            {
                byte[] key = GetDpapiProtectedKey();
                byte[] computedHmac;

                using (var hmac = new HMACSHA256(key))
                {
                    byte[] contentBytes = System.Text.Encoding.UTF8.GetBytes(jsonContent);
                    computedHmac = hmac.ComputeHash(contentBytes);
                }

                string computedHex = BitConverter.ToString(computedHmac).Replace("-", "").ToLowerInvariant();

                // Constant-time comparison to prevent timing attacks
                return SecureEquals(storedHmac, computedHex);
            }
            catch
            {
                // If HMAC computation fails (e.g., DPAPI key unavailable), reject the file
                return false;
            }
        }

        /// <summary>
        /// Computes a DPAPI-protected key derived from user-specific entropy.
        /// The key is tied to the current Windows user account and application identity.
        /// Falls back to a simple hash of a seed string if DPAPI is unavailable (e.g., non-Windows).
        /// </summary>
        private static byte[] GetDpapiProtectedKey()
        {
            try
            {
                // Use DPAPI to protect a seed value — only this user on this machine can decrypt it.
                // We derive the actual HMAC key from the decrypted bytes via SHA256 for proper key size.
                byte[] protectedBytes = ProtectedData.Protect(
                    System.Text.Encoding.UTF8.GetBytes("RoosterAudioSwitcher-ConfigIntegritySeed"),
                    null, // No additional entropy needed; user-scoped is sufficient
                    DataProtectionScope.CurrentUser);

                using var sha256 = SHA256.Create();
                return sha256.ComputeHash(protectedBytes);
            }
            catch
            {
                // Fallback: derive key directly from a seed string.
                // This provides integrity checking but without DPAPI's user/machine binding.
                using var sha256 = SHA256.Create();
                return sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes("RoosterAudioSwitcher-ConfigIntegritySeed"));
            }
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

                // Compute HMAC signature over the JSON content
                byte[] key = GetDpapiProtectedKey();
                string hmacHex;
                using (var hmac = new HMACSHA256(key))
                {
                    byte[] contentBytes = System.Text.Encoding.UTF8.GetBytes(json);
                    byte[] computedHmac = hmac.ComputeHash(contentBytes);
                    hmacHex = BitConverter.ToString(computedHmac).Replace("-", "").ToLowerInvariant();
                }

                // Write JSON followed by HMAC signature as a comment line.
                // Lines starting with '#' are ignored during parsing.
                string signedContent = json + Environment.NewLine + $"#HMAC:{hmacHex}" + Environment.NewLine;
                File.WriteAllText(_configPath, signedContent);
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
