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
            lstDevices!.Items.Clear();
            cmbSwitchDevice!.Items.Clear();
            cmbDefaultDevice!.Items.Clear();
            cmbThirdDevice!.Items.Clear();
            var devices = _audioDeviceManager.GetDevices();

            foreach (var device in devices)
            {
                lstDevices.Items.Add(device);
                cmbSwitchDevice.Items.Add(device);
                cmbDefaultDevice.Items.Add(device);
                cmbThirdDevice.Items.Add(device);
            }

            // Default selections if not yet configured
            if (cmbSwitchDevice.Items.Count > 0 && cmbSwitchDevice.SelectedIndex < 0)
            {
                cmbSwitchDevice.SelectedIndex = 0;
            }

            var currentDefault = devices.Find(d => d.IsDefault);
            if (currentDefault != null)
            {
                SelectComboByDeviceId(cmbDefaultDevice, currentDefault.Id);
            }
            else if (cmbDefaultDevice.Items.Count > 0 && cmbDefaultDevice.SelectedIndex < 0)
            {
                cmbDefaultDevice.SelectedIndex = 0;
            }

            if (cmbThirdDevice.Items.Count > 0 && cmbThirdDevice.SelectedIndex < 0)
            {
                cmbThirdDevice.SelectedIndex = 0;
            }
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
            _configManager.Settings.SwitchToDeviceId = switchDevice.Id;
            _configManager.Settings.DefaultDeviceId = defaultDevice.Id;
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
        /// </summary>
        private void EnableStartupWithWindows()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true) ?? throw new InvalidOperationException("Could not open registry key");
                key.SetValue("RoosterAudioSwitcher", exePath);
                key.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error enabling startup: {ex.Message}", "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Disables startup with Windows by removing the registry entry.
        /// </summary>
        private void DisableStartupWithWindows()
        {
            try
            {
                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true) ?? throw new InvalidOperationException("Could not open registry key");
                key.DeleteValue("RoosterAudioSwitcher", false);
                key.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error disabling startup: {ex.Message}", "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Handles device list refresh button click.
        /// </summary>
        private void BtnRefreshDevices_Click(object? sender, EventArgs e)
        {
            _audioDeviceManager.RefreshDeviceList();
            RefreshDeviceList();
        }

        private void SelectComboByDeviceId(ComboBox? comboBox, string? deviceId)
        {
            if (comboBox == null || string.IsNullOrWhiteSpace(deviceId))
            {
                return;
            }

            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is AudioDevice device && device.Id == deviceId)
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }
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
