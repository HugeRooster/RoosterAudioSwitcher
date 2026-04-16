using System.Collections.Generic;

namespace RoosterAudioSwitcher.Models
{
    /// <summary>
    /// Application settings persisted to JSON config file.
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// Legacy single hotkey setting retained for backward compatibility.
        /// </summary>
        public string HotKey { get; set; } = "Ctrl+Alt+S";

        /// <summary>
        /// Hotkey used to switch to the configured target device.
        /// </summary>
        public string SwitchToDeviceHotKey { get; set; } = "Ctrl+Alt+S";

        /// <summary>
        /// Hotkey used to switch back to the configured default device.
        /// </summary>
        public string ReturnToDefaultHotKey { get; set; } = "Ctrl+Alt+D";

        /// <summary>
        /// Hotkey used to switch to the configured third device.
        /// </summary>
        public string ThirdDeviceHotKey { get; set; } = "Ctrl+Alt+F";

        /// <summary>
        /// Device ID to switch to when the switch hotkey is pressed.
        /// </summary>
        public string? SwitchToDeviceId { get; set; }

        /// <summary>
        /// Device ID to switch back to when the return hotkey is pressed.
        /// </summary>
        public string? DefaultDeviceId { get; set; }

        /// <summary>
        /// Device ID to switch to when the third-device hotkey is pressed.
        /// </summary>
        public string? ThirdDeviceId { get; set; }

        /// <summary>
        /// List of device IDs in the preferred cycle order.
        /// </summary>
        public List<string> DeviceOrder { get; set; } = new();

        /// <summary>
        /// Whether to start the app with Windows.
        /// </summary>
        public bool StartWithWindows { get; set; } = false;

        /// <summary>
        /// Whether to show notifications on device switch.
        /// </summary>
        public bool ShowNotifications { get; set; } = true;

        /// <summary>
        /// Notification display duration in milliseconds.
        /// </summary>
        public int NotificationDurationMs { get; set; } = 3000;

        /// <summary>
        /// Last known default device ID (for persistence across restarts).
        /// </summary>
        public string? LastDefaultDeviceId { get; set; }
    }
}
