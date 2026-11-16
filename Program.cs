using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using RoosterAudioSwitcher.Forms;
using RoosterAudioSwitcher.Managers;

namespace RoosterAudioSwitcher
{
    /// <summary>
    /// Main entry point for RoosterAudioSwitcher application.
    /// Initializes the system tray icon, audio device manager, hotkey handler, and configuration.
    /// </summary>
    internal static class Program
    {
        private static AudioDeviceManager? _audioDeviceManager;
        private static HotkeyManager? _hotkeyManager;
        private static TrayIconManager? _trayIconManager;
        private static NotificationManager? _notificationManager;
        private static ConfigManager? _configManager;
        private static SettingsForm? _settingsForm;
        private static readonly HashSet<string> _reportedUnavailableDeviceIds =
            new(StringComparer.OrdinalIgnoreCase);

        // Held for the lifetime of the process so the GC cannot release our
        // ownership of the single-instance mutex while the app is running.
        private static Mutex? _instanceMutex;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Only one instance may run at a time. A second launch would create a
            // duplicate tray icon and its hotkey registrations would silently fail.
            _instanceMutex = new Mutex(true, @"Local\RoosterAudioSwitcher_SingleInstance", out bool isFirstInstance);
            if (!isFirstInstance)
            {
                MessageBox.Show(
                    "RoosterAudioSwitcher is already running. Look for its icon in the system tray.",
                    "RoosterAudioSwitcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _instanceMutex.Dispose();
                Environment.Exit(0);
            }

            // Enable high-DPI awareness for Windows 10+
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Clear previous log
            Logger.ClearLog();
            Logger.Log("=== RoosterAudioSwitcher Started ===");

            // Initialize managers
            try
            {
                Logger.Log("Main: Creating ConfigManager...");
                _configManager = new ConfigManager();
                Logger.Log($"Main: ConfigManager created. Config file: {_configManager.ConfigPath}");
                
                Logger.Log("Main: Creating AudioDeviceManager...");
                _audioDeviceManager = new AudioDeviceManager();
                
                Logger.Log("Main: Creating HotkeyManager...");
                _hotkeyManager = new HotkeyManager();
                
                Logger.Log("Main: Creating TrayIconManager...");
                _trayIconManager = new TrayIconManager();
                
                Logger.Log("Main: Creating NotificationManager...");
                _notificationManager = new NotificationManager(_trayIconManager);

                // Wire up event handlers
                Logger.Log("Main: Wiring up event handlers...");
                WireUpEventHandlers();

                // Register the initial hotkey from config
                Logger.Log($"Main: Registering switch hotkey: {_configManager.Settings.SwitchToDeviceHotKey}");
                _hotkeyManager.RegisterSwitchHotKey(_configManager.Settings.SwitchToDeviceHotKey);
                Logger.Log($"Main: Registering return hotkey: {_configManager.Settings.ReturnToDefaultHotKey}");
                _hotkeyManager.RegisterReturnHotKey(_configManager.Settings.ReturnToDefaultHotKey);
                Logger.Log($"Main: Registering third hotkey: {_configManager.Settings.ThirdDeviceHotKey}");
                _hotkeyManager.RegisterThirdHotKey(_configManager.Settings.ThirdDeviceHotKey);

                // Load initial device list into tray menu
                Logger.Log("Main: Updating tray menu...");
                UpdateTrayMenu();
                NotifyForUnavailableConfiguredDevices();

                Logger.Log("Main: Running application...");
                // Run the application
                Application.Run();
            }
            catch (Exception ex)
            {
                Logger.LogError("Main: Fatal error during initialization", ex);
                MessageBox.Show($"Fatal error during initialization: {ex.Message}",
                    "RoosterAudioSwitcher - Startup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                // Clean up
                CleanupManagers();
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Wires up event handlers between managers.
        /// </summary>
        private static void WireUpEventHandlers()
        {
            if (_hotkeyManager == null || _audioDeviceManager == null || _trayIconManager == null || _notificationManager == null)
                throw new InvalidOperationException("Managers not initialized");

            Logger.Log("WireUpEventHandlers: Starting...");

            // Switch hotkey pressed -> switch to configured target device.
            _hotkeyManager.SwitchHotKeyPressed += (s, e) =>
            {
                try
                {
                    Logger.Log(">>> SWITCH HOTKEY HANDLER FIRED <<<");
                    var settings = _configManager?.Settings;
                    var targetDevice = settings == null
                        ? null
                        : ResolveConfiguredDevice(
                            settings.SwitchToDeviceId,
                            settings.SwitchToDeviceName,
                            device =>
                            {
                                settings.SwitchToDeviceId = device.Id;
                                settings.SwitchToDeviceName = device.FriendlyName;
                            });
                    if (targetDevice == null)
                    {
                        Logger.LogError("Switch hotkey: target device is not configured or not found.");
                        return;
                    }

                    Logger.Log($"Switch hotkey: switching to {targetDevice.FriendlyName}");
                    if (_audioDeviceManager.SetDefaultDevice(targetDevice))
                    {
                        UpdateTrayMenu();

                        if (_configManager?.Settings.ShowNotifications ?? false)
                        {
                            _notificationManager.ShowDeviceChangeNotification(targetDevice.FriendlyName);
                        }
                    }
                    else
                    {
                        Logger.LogError("Switch hotkey: failed to switch device");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Switch hotkey: unhandled exception", ex);
                }
            };

            // Return hotkey pressed -> switch back to configured default device.
            _hotkeyManager.ReturnHotKeyPressed += (s, e) =>
            {
                try
                {
                    Logger.Log(">>> RETURN HOTKEY HANDLER FIRED <<<");
                    var settings = _configManager?.Settings;
                    var defaultDevice = settings == null
                        ? null
                        : ResolveConfiguredDevice(
                            settings.DefaultDeviceId,
                            settings.DefaultDeviceName,
                            device =>
                            {
                                settings.DefaultDeviceId = device.Id;
                                settings.DefaultDeviceName = device.FriendlyName;
                            });
                    if (defaultDevice == null)
                    {
                        Logger.LogError("Return hotkey: default device is not configured or not found.");
                        return;
                    }

                    Logger.Log($"Return hotkey: switching to {defaultDevice.FriendlyName}");
                    if (_audioDeviceManager.SetDefaultDevice(defaultDevice))
                    {
                        UpdateTrayMenu();

                        if (_configManager?.Settings.ShowNotifications ?? false)
                        {
                            _notificationManager.ShowDeviceChangeNotification(defaultDevice.FriendlyName);
                        }
                    }
                    else
                    {
                        Logger.LogError("Return hotkey: failed to switch device");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Return hotkey: unhandled exception", ex);
                }
            };

            // Third hotkey pressed -> switch to configured third device.
            _hotkeyManager.ThirdHotKeyPressed += (s, e) =>
            {
                try
                {
                    Logger.Log(">>> THIRD HOTKEY HANDLER FIRED <<<");
                    var settings = _configManager?.Settings;
                    var thirdDevice = settings == null
                        ? null
                        : ResolveConfiguredDevice(
                            settings.ThirdDeviceId,
                            settings.ThirdDeviceName,
                            device =>
                            {
                                settings.ThirdDeviceId = device.Id;
                                settings.ThirdDeviceName = device.FriendlyName;
                            });
                    if (thirdDevice == null)
                    {
                        Logger.LogError("Third hotkey: third device is not configured or not found.");
                        return;
                    }

                    Logger.Log($"Third hotkey: switching to {thirdDevice.FriendlyName}");
                    if (_audioDeviceManager.SetDefaultDevice(thirdDevice))
                    {
                        UpdateTrayMenu();

                        if (_configManager?.Settings.ShowNotifications ?? false)
                        {
                            _notificationManager.ShowDeviceChangeNotification(thirdDevice.FriendlyName);
                        }
                    }
                    else
                    {
                        Logger.LogError("Third hotkey: failed to switch device");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Third hotkey: unhandled exception", ex);
                }
            };

            // Audio device list changed → update tray menu
            _audioDeviceManager.DevicesChanged += (s, e) =>
            {
                Logger.Log("DevicesChanged event fired");
                UpdateTrayMenu();
                NotifyForUnavailableConfiguredDevices();
            };

            // Tray menu: Settings clicked
            _trayIconManager.SettingsRequested += (s, e) =>
            {
                Logger.Log("Settings requested");
                ShowSettingsForm();
            };

            // Tray menu: Device selected
            _trayIconManager.DeviceSelected += (s, e) =>
            {
                Logger.Log($"Device selected from tray: {e.DeviceId}");
                var device = _audioDeviceManager.GetDevices().FirstOrDefault(d => d.Id == e.DeviceId);
                if (device != null)
                {
                    Logger.Log($"Setting device: {device.FriendlyName}");
                    _audioDeviceManager.SetDefaultDevice(device);
                    UpdateTrayMenu();

                    // Show notification if enabled
                    if (_configManager?.Settings.ShowNotifications ?? false)
                    {
                        _notificationManager.ShowDeviceChangeNotification(device.FriendlyName);
                    }
                }
                else
                {
                    Logger.LogError($"Device {e.DeviceId} not found in device list");
                }
            };

            // Tray menu: Exit clicked
            _trayIconManager.ExitRequested += (s, e) =>
            {
                Logger.Log("Exit requested");
                CleanupManagers();
                Application.Exit();
            };
        }

        /// <summary>
        /// Updates the tray icon context menu with current device list.
        /// </summary>
        private static void UpdateTrayMenu()
        {
            Logger.Log("UpdateTrayMenu: Updating tray menu...");
            if (_audioDeviceManager == null || _trayIconManager == null)
            {
                Logger.LogError("UpdateTrayMenu: AudioDeviceManager or TrayIconManager is null");
                return;
            }

            var devices = _audioDeviceManager.GetDevices();
            Logger.Log($"UpdateTrayMenu: Found {devices.Count} devices");
            var currentDefault = devices.FirstOrDefault(d => d.IsDefault);
            if (currentDefault != null)
            {
                Logger.Log($"UpdateTrayMenu: Current default device: {currentDefault.FriendlyName}");
            }
            _trayIconManager.UpdateDeviceList(devices, currentDefault?.Id);
        }

        /// <summary>
        /// Shows the settings form dialog.
        /// </summary>
        private static void ShowSettingsForm()
        {
            Logger.Log("ShowSettingsForm: Opening settings dialog...");
            if (_configManager == null || _audioDeviceManager == null || _hotkeyManager == null)
            {
                Logger.LogError("ShowSettingsForm: Managers not initialized");
                return;
            }

            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                Logger.Log("ShowSettingsForm: Creating new SettingsForm");
                _settingsForm = new SettingsForm(_configManager, _audioDeviceManager, _hotkeyManager);
            }

            _settingsForm.ShowDialog();
            NotifyForUnavailableConfiguredDevices();
        }

        /// <summary>
        /// Cleans up all managers and releases resources.
        /// </summary>
        private static void CleanupManagers()
        {
            Logger.Log("CleanupManagers: Starting cleanup...");
            _settingsForm?.Dispose();
            _trayIconManager?.Dispose();
            Logger.Log("CleanupManagers: Unregistering hotkey...");
            _hotkeyManager?.Dispose();
            _audioDeviceManager?.Dispose();
            Logger.Log("=== RoosterAudioSwitcher Stopped ===");
        }

        private static Models.AudioDevice? ResolveConfiguredDevice(
            string? deviceId,
            string? friendlyName,
            Action<Models.AudioDevice> updateConfiguration)
        {
            if (string.IsNullOrWhiteSpace(deviceId) || _audioDeviceManager == null)
            {
                return null;
            }

            var devices = _audioDeviceManager.GetDevices();
            var deviceById = devices.FirstOrDefault(d =>
                string.Equals(d.Id, deviceId, StringComparison.OrdinalIgnoreCase));
            if (deviceById != null)
            {
                if (!string.Equals(
                    deviceById.FriendlyName,
                    friendlyName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    updateConfiguration(deviceById);
                    _configManager?.SaveSettings();
                }

                return deviceById;
            }

            if (string.IsNullOrWhiteSpace(friendlyName))
            {
                return null;
            }

            var nameMatches = devices
                .Where(d => string.Equals(d.FriendlyName, friendlyName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (nameMatches.Count != 1)
            {
                if (nameMatches.Count > 1)
                {
                    Logger.LogWarning(
                        $"Configured device ID {deviceId} is unavailable and friendly name '{friendlyName}' matches multiple devices; leaving the saved ID unchanged.");
                }

                return null;
            }

            var replacement = nameMatches[0];
            updateConfiguration(replacement);
            _configManager?.SaveSettings();
            Logger.Log(
                $"Reidentified configured device '{friendlyName}' with new ID {replacement.Id}.");
            return replacement;
        }

        private static void NotifyForUnavailableConfiguredDevices()
        {
            if (_configManager == null || _audioDeviceManager == null || _notificationManager == null)
            {
                return;
            }

            var settings = _configManager.Settings;
            var configuredDevices = new[]
            {
                (Label: "Switch-to", Id: settings.SwitchToDeviceId, Name: settings.SwitchToDeviceName,
                    Update: (Action<Models.AudioDevice>)(device =>
                    {
                        settings.SwitchToDeviceId = device.Id;
                        settings.SwitchToDeviceName = device.FriendlyName;
                    })),
                (Label: "Default", Id: settings.DefaultDeviceId, Name: settings.DefaultDeviceName,
                    Update: (Action<Models.AudioDevice>)(device =>
                    {
                        settings.DefaultDeviceId = device.Id;
                        settings.DefaultDeviceName = device.FriendlyName;
                    })),
                (Label: "Third", Id: settings.ThirdDeviceId, Name: settings.ThirdDeviceName,
                    Update: (Action<Models.AudioDevice>)(device =>
                    {
                        settings.ThirdDeviceId = device.Id;
                        settings.ThirdDeviceName = device.FriendlyName;
                    }))
            };
            var unavailable = configuredDevices
                .Where(device => !string.IsNullOrWhiteSpace(device.Id) &&
                    ResolveConfiguredDevice(device.Id, device.Name, device.Update) == null)
                .ToList();
            var unavailableIds = new HashSet<string>(
                unavailable.Select(device => device.Id!),
                StringComparer.OrdinalIgnoreCase);

            _reportedUnavailableDeviceIds.IntersectWith(unavailableIds);
            var newlyUnavailable = unavailable
                .Where(device => !_reportedUnavailableDeviceIds.Contains(device.Id!))
                .ToList();

            if (newlyUnavailable.Count == 0)
            {
                return;
            }

            foreach (var device in newlyUnavailable)
            {
                _reportedUnavailableDeviceIds.Add(device.Id!);
            }

            string labels = string.Join(", ", newlyUnavailable.Select(device => device.Label).Distinct());
            _notificationManager.ShowNotification(
                "Configured audio device unavailable",
                $"{labels} device not found. Reconnect it or choose a replacement in Settings.");
        }
    }
}
