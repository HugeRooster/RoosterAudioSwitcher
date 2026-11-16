using System;
using System.Windows.Forms;
using RoosterAudioSwitcher.Managers;
using RoosterAudioSwitcher.Models;

namespace RoosterAudioSwitcher.Forms
{
    /// <summary>
    /// Settings dialog for configuring RoosterAudioSwitcher.
    /// Allows users to view devices, change hotkey, and toggle startup with Windows.
    /// </summary>
    public partial class SettingsForm : Form
    {
        private readonly ConfigManager _configManager;
        private readonly AudioDeviceManager _audioDeviceManager;
        private readonly HotkeyManager _hotkeyManager;

        public SettingsForm(ConfigManager configManager, AudioDeviceManager audioDeviceManager, HotkeyManager hotkeyManager)
        {
            _configManager = configManager;
            _audioDeviceManager = audioDeviceManager;
            _hotkeyManager = hotkeyManager;

            InitializeComponent();
            ApplyWindowIcon();
            LoadSettings();
        }

        private void ApplyWindowIcon()
        {
            try
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null)
                {
                    Icon = icon;
                }
            }
            catch
            {
                // Keep default icon if extraction fails.
            }
        }

        /// <summary>
        /// Loads current settings into the UI.
        /// </summary>
        private void LoadSettings()
        {
            // Load hotkeys
            txtSwitchHotKey!.Text = _configManager.Settings.SwitchToDeviceHotKey;
            txtReturnHotKey!.Text = _configManager.Settings.ReturnToDefaultHotKey;
            txtThirdHotKey!.Text = _configManager.Settings.ThirdDeviceHotKey;

            // Load startup with Windows setting
            chkStartWithWindows!.Checked = _configManager.Settings.StartWithWindows;

            // Load device list
            RefreshDeviceList();

            // Load selected device IDs once list is populated
            if (!string.IsNullOrWhiteSpace(_configManager.Settings.SwitchToDeviceId))
            {
                SelectComboByDeviceId(cmbSwitchDevice, _configManager.Settings.SwitchToDeviceId);
            }

            if (!string.IsNullOrWhiteSpace(_configManager.Settings.DefaultDeviceId))
            {
                SelectComboByDeviceId(cmbDefaultDevice, _configManager.Settings.DefaultDeviceId);
            }

            if (!string.IsNullOrWhiteSpace(_configManager.Settings.ThirdDeviceId))
            {
                SelectComboByDeviceId(cmbThirdDevice, _configManager.Settings.ThirdDeviceId);
            }

            // Load notification setting
            chkShowNotifications!.Checked = _configManager.Settings.ShowNotifications;
        }

        /// <summary>
        /// Refreshes the device list display.
        /// </summary>
        private void RefreshDeviceList()
        {
            // Remember the current selections so a refresh does not clobber
            // choices the user made but has not saved yet.
            AudioDevice? switchSelection = cmbSwitchDevice!.SelectedItem as AudioDevice;
            AudioDevice? defaultSelection = cmbDefaultDevice!.SelectedItem as AudioDevice;
            AudioDevice? thirdSelection = cmbThirdDevice!.SelectedItem as AudioDevice;

            lstDevices!.Items.Clear();
            var devices = _audioDeviceManager.GetDevices();

            foreach (var device in devices)
            {
                lstDevices.Items.Add(device);
            }

            PopulateDeviceCombo(
                cmbSwitchDevice,
                devices,
                switchSelection,
                _configManager.Settings.SwitchToDeviceId,
                _configManager.Settings.SwitchToDeviceName,
                "Switch-to");
            PopulateDeviceCombo(
                cmbDefaultDevice,
                devices,
                defaultSelection,
                _configManager.Settings.DefaultDeviceId,
                _configManager.Settings.DefaultDeviceName,
                "Default",
                devices.Find(d => d.IsDefault)?.Id);
            PopulateDeviceCombo(
                cmbThirdDevice,
                devices,
                thirdSelection,
                _configManager.Settings.ThirdDeviceId,
                _configManager.Settings.ThirdDeviceName,
                "Third");
        }

        private static void PopulateDeviceCombo(
            ComboBox comboBox,
            System.Collections.Generic.List<AudioDevice> devices,
            AudioDevice? previousSelection,
            string? savedDeviceId,
            string? savedDeviceName,
            string deviceLabel,
            string? fallbackDeviceId = null)
        {
            comboBox.Items.Clear();

            foreach (var device in devices)
            {
                comboBox.Items.Add(device);
            }

            AddUnavailableSelection(comboBox, previousSelection, deviceLabel);
            if (!string.IsNullOrWhiteSpace(savedDeviceId) &&
                !ContainsDeviceId(comboBox, savedDeviceId))
            {
                comboBox.Items.Add(new AudioDevice
                {
                    Id = savedDeviceId,
                    FriendlyName = string.IsNullOrWhiteSpace(savedDeviceName)
                        ? $"{deviceLabel} device"
                        : savedDeviceName,
                    IsAvailable = false
                });
            }

            string? preferredId = previousSelection?.Id ?? savedDeviceId ?? fallbackDeviceId;
            if (SelectComboByDeviceId(comboBox, preferredId))
            {
                return;
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private static void AddUnavailableSelection(
            ComboBox comboBox,
            AudioDevice? previousSelection,
            string deviceLabel)
        {
            if (previousSelection == null ||
                ContainsDeviceId(comboBox, previousSelection.Id))
            {
                return;
            }

            comboBox.Items.Add(new AudioDevice
            {
                Id = previousSelection.Id,
                FriendlyName = previousSelection.IsAvailable
                    ? previousSelection.FriendlyName
                    : deviceLabel + " device",
                IsAvailable = false
            });
        }

        private static bool ContainsDeviceId(ComboBox comboBox, string deviceId)
        {
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is AudioDevice device &&
                    string.Equals(device.Id, deviceId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Saves current settings and closes the form.
        /// </summary>
        private void BtnSave_Click(object? sender, EventArgs e)
        {
            // Validate and save hotkeys
            string switchHotKey = txtSwitchHotKey?.Text?.Trim() ?? string.Empty;
            string returnHotKey = txtReturnHotKey?.Text?.Trim() ?? string.Empty;
            string thirdHotKey = txtThirdHotKey?.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(switchHotKey) || string.IsNullOrWhiteSpace(returnHotKey) || string.IsNullOrWhiteSpace(thirdHotKey))
            {
                MessageBox.Show("All three hotkeys are required.", "Invalid Hotkey", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.Equals(switchHotKey, returnHotKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(switchHotKey, thirdHotKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(returnHotKey, thirdHotKey, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Switch, Return, and Third hotkeys must all be different.", "Invalid Hotkey", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbSwitchDevice?.SelectedItem is not AudioDevice switchDevice)
            {
                MessageBox.Show("Please select the device to switch to.", "Missing Device", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbDefaultDevice?.SelectedItem is not AudioDevice defaultDevice)
            {
                MessageBox.Show("Please select the default device to return to.", "Missing Device", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbThirdDevice?.SelectedItem is not AudioDevice thirdDevice)
            {
                MessageBox.Show("Please select the third device.", "Missing Device", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Try to register both hotkeys
            if (!_hotkeyManager.RegisterSwitchHotKey(switchHotKey))
            {
                MessageBox.Show("Failed to register switch hotkey. Please use format like 'Ctrl+Alt+S'.", "Invalid Hotkey", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!_hotkeyManager.RegisterReturnHotKey(returnHotKey))
            {
                MessageBox.Show("Failed to register return hotkey. Please use format like 'Ctrl+Alt+D'.", "Invalid Hotkey", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!_hotkeyManager.RegisterThirdHotKey(thirdHotKey))
            {
                MessageBox.Show("Failed to register third hotkey. Please use format like 'Ctrl+Alt+F'.", "Invalid Hotkey", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Save settings
            _configManager.Settings.SwitchToDeviceHotKey = switchHotKey;
            _configManager.Settings.ReturnToDefaultHotKey = returnHotKey;
            _configManager.Settings.ThirdDeviceHotKey = thirdHotKey;
            _configManager.Settings.HotKey = switchHotKey;
            if (switchDevice.IsAvailable ||
                !string.Equals(
                    switchDevice.Id,
                    _configManager.Settings.SwitchToDeviceId,
                    StringComparison.OrdinalIgnoreCase))
            {
                _configManager.Settings.SwitchToDeviceName = switchDevice.FriendlyName;
            }
            _configManager.Settings.SwitchToDeviceId = switchDevice.Id;
            if (defaultDevice.IsAvailable ||
                !string.Equals(
                    defaultDevice.Id,
                    _configManager.Settings.DefaultDeviceId,
                    StringComparison.OrdinalIgnoreCase))
            {
                _configManager.Settings.DefaultDeviceName = defaultDevice.FriendlyName;
            }
            _configManager.Settings.DefaultDeviceId = defaultDevice.Id;
            if (thirdDevice.IsAvailable ||
                !string.Equals(
                    thirdDevice.Id,
                    _configManager.Settings.ThirdDeviceId,
                    StringComparison.OrdinalIgnoreCase))
            {
                _configManager.Settings.ThirdDeviceName = thirdDevice.FriendlyName;
            }
            _configManager.Settings.ThirdDeviceId = thirdDevice.Id;
            _configManager.Settings.StartWithWindows = chkStartWithWindows?.Checked ?? false;
            _configManager.Settings.ShowNotifications = chkShowNotifications?.Checked ?? false;

            // Apply startup setting
            if (chkStartWithWindows?.Checked == true)
            {
                EnableStartupWithWindows();
            }
            else
            {
                DisableStartupWithWindows();
            }

            _configManager.SaveSettings();

            MessageBox.Show("Settings saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// Cancels and closes the form without saving.
        /// </summary>
        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// Enables startup with Windows by adding a registry entry.
        /// Verifies the value was written correctly after setting it.
        /// </summary>
        private void EnableStartupWithWindows()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true) ?? throw new InvalidOperationException("Could not open registry key");

                // Sanity-check the executable path before writing it to the registry.
                if (!IsValidRegistryPath(exePath))
                {
                    MessageBox.Show("Invalid application path. Cannot enable startup.", "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Run key values must be quoted when the path contains spaces,
                // otherwise Windows cannot launch the app at startup.
                string startupValue = exePath.Contains(' ') ? $"\"{exePath}\"" : exePath;
                key.SetValue("RoosterAudioSwitcher", startupValue);
                key.Close();

                // Verify the registry value was written correctly.
                if (!VerifyRegistryValue("RoosterAudioSwitcher", startupValue))
                {
                    MessageBox.Show(
                        "Startup entry was created but could not be verified. The application may still start with Windows, but check your settings.",
                        "Startup Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error enabling startup: {ex.Message}", "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Disables startup with Windows by removing the registry entry.
        /// Verifies the value was removed after deletion.
        /// </summary>
        private void DisableStartupWithWindows()
        {
            try
            {
                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true) ?? throw new InvalidOperationException("Could not open registry key");

                // Check if the value exists before attempting deletion.
                object? existingValue = key.GetValue("RoosterAudioSwitcher");
                if (existingValue != null)
                {
                    key.DeleteValue("RoosterAudioSwitcher", false);
                }

                key.Close();

                // Verify the registry value was removed.
                if (VerifyRegistryValue("RoosterAudioSwitcher", null))
                {
                    MessageBox.Show(
                        "Startup entry could not be removed. It may be managed by group policy.",
                        "Startup Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error disabling startup: {ex.Message}", "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Verifies that a registry value exists and matches the expected value.
        /// Returns true if verification passes (value matches when expectedValue is non-null, or value doesn't exist when expectedValue is null).
        /// </summary>
        private static bool VerifyRegistryValue(string valueName, string? expectedValue)
        {
            try
            {
                Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false);

                if (key == null)
                    return false;

                object? actualValue = key.GetValue(valueName);
                key.Close();

                if (expectedValue == null)
                {
                    // Expecting the value to NOT exist.
                    return actualValue == null;
                }

                // Expecting the value to exist and match.
                string? actualString = actualValue as string;
                return string.Equals(actualString, expectedValue, StringComparison.Ordinal);
            }
            catch
            {
                // If verification fails due to any error (e.g., access denied), assume failure.
                return false;
            }
        }

        /// <summary>
        /// Validates that a file path is safe to write to the registry.
        /// Checks for null, empty, and extreme length.
        /// </summary>
        private static bool IsValidRegistryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            // Registry values have practical limits (~32KB), but we use a much stricter limit.
            if (path.Length > 1024)
                return false;

            return true;
        }

        /// <summary>
        /// Handles device list refresh button click.
        /// </summary>
        private void BtnRefreshDevices_Click(object? sender, EventArgs e)
        {
            _audioDeviceManager.RefreshDeviceList();
            RefreshDeviceList();
        }

        private static bool SelectComboByDeviceId(ComboBox? comboBox, string? deviceId)
        {
            if (comboBox == null || string.IsNullOrWhiteSpace(deviceId))
            {
                return false;
            }

            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is AudioDevice device &&
                    string.Equals(device.Id, deviceId, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = i;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Handles test button to immediately switch to next device.
        /// </summary>
        private void BtnTestSwitch_Click(object? sender, EventArgs e)
        {
            var nextDevice = _audioDeviceManager.CycleToNextDevice();
            if (nextDevice != null)
            {
                MessageBox.Show($"Switched to: {nextDevice.FriendlyName}", "Device Switched", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshDeviceList();
            }
            else
            {
                MessageBox.Show("No audio devices available.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
