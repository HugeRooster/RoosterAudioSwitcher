using System;
using System.Linq;
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

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
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

                Logger.Log("Main: Running application...");
                // Run the application
                Application.Run();
            }
            catch (Exception ex)
            {
                Logger.LogError("Main: Fatal error during initialization", ex);
                MessageBox.Show($"Fatal error during initialization: {ex.Message}\n\n{ex.StackTrace}",
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
                Logger.Log(">>> SWITCH HOTKEY HANDLER FIRED <<<");
                var targetDevice = ResolveDeviceById(_configManager?.Settings.SwitchToDeviceId);
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
                        _trayIconManager.ShowBalloonTip("Audio Device Changed", $"Now playing through: {targetDevice.FriendlyName}");
                    }
                }
                else
                {
                    Logger.LogError("Switch hotkey: failed to switch device");
                }
            };

            // Return hotkey pressed -> switch back to configured default device.
            _hotkeyManager.ReturnHotKeyPressed += (s, e) =>
            {
                Logger.Log(">>> RETURN HOTKEY HANDLER FIRED <<<");
                var defaultDevice = ResolveDeviceById(_configManager?.Settings.DefaultDeviceId);
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
                        _trayIconManager.ShowBalloonTip("Audio Device Changed", $"Now playing through: {defaultDevice.FriendlyName}");
                    }
                }
                else
                {
                    Logger.LogError("Return hotkey: failed to switch device");
                }
            };

            // Third hotkey pressed -> switch to configured third device.
            _hotkeyManager.ThirdHotKeyPressed += (s, e) =>
            {
                Logger.Log(">>> THIRD HOTKEY HANDLER FIRED <<<");
                var thirdDevice = ResolveDeviceById(_configManager?.Settings.ThirdDeviceId);
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
                        _trayIconManager.ShowBalloonTip("Audio Device Changed", $"Now playing through: {thirdDevice.FriendlyName}");
                    }
                }
                else
                {
                    Logger.LogError("Third hotkey: failed to switch device");
                }
            };

            // Audio device list changed → update tray menu
            _audioDeviceManager.DevicesChanged += (s, e) =>
            {
                Logger.Log("DevicesChanged event fired");
                UpdateTrayMenu();
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

        private static Models.AudioDevice? ResolveDeviceById(string? deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId) || _audioDeviceManager == null)
            {
                return null;
            }

            return _audioDeviceManager.GetDevices().FirstOrDefault(d => d.Id == deviceId);
        }
    }
}
