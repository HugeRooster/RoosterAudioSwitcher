using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RoosterAudioSwitcher.Models;

namespace RoosterAudioSwitcher.Managers
{
    /// <summary>
    /// Manages the system tray icon, context menu, and related UI interactions.
    /// </summary>
    public class TrayIconManager : IDisposable
    {
        private NotifyIcon? _trayIcon;
        private ContextMenuStrip? _contextMenu;
        private ToolStripMenuItem? _devicesMenu;
        private ToolStripSeparator? _devicesSeparator;

        /// <summary>
        /// Fired when the user requests to open the settings window.
        /// </summary>
        public event EventHandler? SettingsRequested;

        /// <summary>
        /// Fired when the user requests to switch to a specific device.
        /// </summary>
        public event EventHandler<DeviceMenuItemClickedEventArgs>? DeviceSelected;

        /// <summary>
        /// Fired when the user requests to exit the application.
        /// </summary>
        public event EventHandler? ExitRequested;

        public TrayIconManager()
        {
            InitializeTrayIcon();
        }

        /// <summary>
        /// Initializes the system tray icon and context menu.
        /// </summary>
        private void InitializeTrayIcon()
        {
            try
            {
                // Create tray icon
                _trayIcon = new NotifyIcon
                {
                    Visible = true,
                    Text = "Rooster Audio Switcher",
                    Icon = GetApplicationIcon()
                };

                // Create context menu
                _contextMenu = new ContextMenuStrip();

                // Current Device Label (disabled, just for display)
                var currentDeviceItem = new ToolStripMenuItem("Current Device: Initializing...");
                currentDeviceItem.Enabled = false;
                currentDeviceItem.Name = "CurrentDeviceLabel";
                _contextMenu.Items.Add(currentDeviceItem);

                // Separator
                _contextMenu.Items.Add(new ToolStripSeparator());

                // Devices submenu
                _devicesMenu = new ToolStripMenuItem("Switch Device");
                _contextMenu.Items.Add(_devicesMenu);

                // Separator
                _devicesSeparator = new ToolStripSeparator();
                _contextMenu.Items.Add(_devicesSeparator);

                // Settings option
                var settingsItem = new ToolStripMenuItem("Settings");
                settingsItem.Click += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
                _contextMenu.Items.Add(settingsItem);

                // Exit option
                var exitItem = new ToolStripMenuItem("Exit");
                exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
                _contextMenu.Items.Add(exitItem);

                // Attach context menu to tray icon
                _trayIcon.ContextMenuStrip = _contextMenu;

                // Also allow double-click to open settings
                _trayIcon.DoubleClick += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing tray icon: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the context menu with the list of available devices.
        /// </summary>
        public void UpdateDeviceList(List<AudioDevice> devices, string? currentDeviceId = null)
        {
            try
            {
                if (_devicesMenu == null || _contextMenu == null)
                    return;

                // Update current device label
                var currentDeviceItem = _contextMenu.Items.Cast<ToolStripItem>()
                    .FirstOrDefault(i => i.Name == "CurrentDeviceLabel") as ToolStripMenuItem;
                if (currentDeviceItem != null && devices.Count > 0)
                {
                    var current = devices.FirstOrDefault(d => d.IsDefault);
                    currentDeviceItem.Text = $"Current Device: {current?.FriendlyName ?? "Unknown"}";
                    _trayIcon!.Text = $"Rooster Audio Switcher\n{current?.FriendlyName ?? "No device"}";
                }

                // Clear existing device items
                _devicesMenu.DropDownItems.Clear();

                // Add device items
                foreach (var device in devices)
                {
                    var deviceItem = new ToolStripMenuItem(device.FriendlyName)
                    {
                        Tag = device.Id,
                        Checked = device.IsDefault
                    };
                    deviceItem.Click += (s, e) => DeviceSelected?.Invoke(this, new DeviceMenuItemClickedEventArgs(device.Id));
                    _devicesMenu.DropDownItems.Add(deviceItem);
                }

                // Show/hide devices menu based on device availability
                _devicesMenu.Visible = devices.Count > 0;
                _devicesSeparator!.Visible = devices.Count > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating device list: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the tooltip text on the tray icon.
        /// </summary>
        public void SetToolTip(string text)
        {
            if (_trayIcon != null)
            {
                _trayIcon.Text = text;
            }
        }

        /// <summary>
        /// Shows a balloon tip notification in the tray area.
        /// </summary>
        public void ShowBalloonTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Info, int durationMs = 3000)
        {
            try
            {
                if (_trayIcon != null)
                {
                    _trayIcon.ShowBalloonTip(durationMs, title, text, icon);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing balloon tip: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the application icon, or creates a default one if not found.
        /// </summary>
        private Icon GetApplicationIcon()
        {
            try
            {
                // Try to load icon from Resources folder
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icon.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading icon: {ex.Message}");
            }

            // Return default application icon as fallback
            return SystemIcons.Application;
        }

        public void Dispose()
        {
            _contextMenu?.Dispose();
            _trayIcon?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Event args for when a device is selected from the context menu.
    /// </summary>
    public class DeviceMenuItemClickedEventArgs : EventArgs
    {
        public string DeviceId { get; }

        public DeviceMenuItemClickedEventArgs(string deviceId)
        {
            DeviceId = deviceId;
        }
    }
}
