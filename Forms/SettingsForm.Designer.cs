#nullable enable

using System;
using System.Windows.Forms;
using System.Drawing;

namespace RoosterAudioSwitcher.Forms
{
    partial class SettingsForm
    {
        private System.ComponentModel.IContainer? components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            
            // Form properties
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(500, 500);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Rooster Audio Switcher - Settings";
            Icon = SystemIcons.Application;

            // Create tab control for organization
            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(10)
            };

            // ===== Hotkey Tab =====
            var tabHotkey = new TabPage("Hotkey");
            var lblSwitchHotkey = new Label
            {
                Text = "Switch Hotkey (switch to selected device):",
                AutoSize = true,
                Location = new Point(10, 20)
            };
            txtSwitchHotKey = new TextBox
            {
                Location = new Point(10, 50),
                Width = 200,
                Text = "Ctrl+Alt+S"
            };
            var lblReturnHotkey = new Label
            {
                Text = "Return Hotkey (switch back to default device):",
                AutoSize = true,
                Location = new Point(10, 90)
            };
            txtReturnHotKey = new TextBox
            {
                Location = new Point(10, 120),
                Width = 200,
                Text = "Ctrl+Alt+D"
            };
            var lblThirdHotkey = new Label
            {
                Text = "Third Device Hotkey:",
                AutoSize = true,
                Location = new Point(10, 160)
            };
            txtThirdHotKey = new TextBox
            {
                Location = new Point(10, 190),
                Width = 200,
                Text = "Ctrl+Alt+F"
            };
            var lblHotkeyHelp = new Label
            {
                Text = "Supported modifiers: Ctrl, Alt, Shift, Windows",
                AutoSize = true,
                Location = new Point(10, 230),
                ForeColor = Color.Gray,
                Font = new Font(Font, FontStyle.Italic)
            };
            tabHotkey.Controls.Add(lblSwitchHotkey);
            tabHotkey.Controls.Add(txtSwitchHotKey);
            tabHotkey.Controls.Add(lblReturnHotkey);
            tabHotkey.Controls.Add(txtReturnHotKey);
            tabHotkey.Controls.Add(lblThirdHotkey);
            tabHotkey.Controls.Add(txtThirdHotKey);
            tabHotkey.Controls.Add(lblHotkeyHelp);

            // ===== Devices Tab =====
            var tabDevices = new TabPage("Devices");
            var lblDevices = new Label
            {
                Text = "Available Audio Devices:",
                AutoSize = true,
                Location = new Point(10, 20)
            };
            var lblSwitchDevice = new Label
            {
                Text = "Switch-to device:",
                AutoSize = true,
                Location = new Point(10, 50)
            };
            cmbSwitchDevice = new ComboBox
            {
                Location = new Point(120, 46),
                Width = 350,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            var lblDefaultDevice = new Label
            {
                Text = "Default device:",
                AutoSize = true,
                Location = new Point(10, 82)
            };
            cmbDefaultDevice = new ComboBox
            {
                Location = new Point(120, 78),
                Width = 350,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            var lblThirdDevice = new Label
            {
                Text = "Third device:",
                AutoSize = true,
                Location = new Point(10, 114)
            };
            cmbThirdDevice = new ComboBox
            {
                Location = new Point(120, 110),
                Width = 350,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            lstDevices = new ListBox
            {
                Location = new Point(10, 146),
                Width = 460,
                Height = 106
            };
            BtnRefreshDevices = new Button
            {
                Text = "Refresh Devices",
                Location = new Point(10, 260),
                Width = 100
            };
            BtnRefreshDevices.Click += BtnRefreshDevices_Click;
            BtnTestSwitch = new Button
            {
                Text = "Test Switch Next",
                Location = new Point(120, 260),
                Width = 120
            };
            BtnTestSwitch.Click += BtnTestSwitch_Click;
            tabDevices.Controls.Add(lblDevices);
            tabDevices.Controls.Add(lblSwitchDevice);
            tabDevices.Controls.Add(cmbSwitchDevice);
            tabDevices.Controls.Add(lblDefaultDevice);
            tabDevices.Controls.Add(cmbDefaultDevice);
            tabDevices.Controls.Add(lblThirdDevice);
            tabDevices.Controls.Add(cmbThirdDevice);
            tabDevices.Controls.Add(lstDevices);
            tabDevices.Controls.Add(BtnRefreshDevices);
            tabDevices.Controls.Add(BtnTestSwitch);

            // ===== Options Tab =====
            var tabOptions = new TabPage("Options");
            chkStartWithWindows = new CheckBox
            {
                Text = "Start with Windows",
                Location = new Point(10, 20),
                Width = 200,
                AutoSize = true
            };
            chkShowNotifications = new CheckBox
            {
                Text = "Show notifications on device change",
                Location = new Point(10, 50),
                Width = 250,
                AutoSize = true,
                Checked = true
            };
            tabOptions.Controls.Add(chkStartWithWindows);
            tabOptions.Controls.Add(chkShowNotifications);

            // Add tabs to tab control
            tabControl.TabPages.Add(tabHotkey);
            tabControl.TabPages.Add(tabDevices);
            tabControl.TabPages.Add(tabOptions);

            // ===== Buttons =====
            var pnlButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BorderStyle = BorderStyle.FixedSingle
            };

            BtnSave = new Button
            {
                Text = "Save",
                Location = new Point(310, 10),
                Width = 80,
                Height = 30,
                DialogResult = DialogResult.OK
            };
            BtnSave.Click += BtnSave_Click;

            BtnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(400, 10),
                Width = 80,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };
            BtnCancel.Click += BtnCancel_Click;

            pnlButtons.Controls.Add(BtnSave);
            pnlButtons.Controls.Add(BtnCancel);

            // Add controls to form
            Controls.Add(tabControl);
            Controls.Add(pnlButtons);
        }

        private TextBox? txtSwitchHotKey;
        private TextBox? txtReturnHotKey;
        private TextBox? txtThirdHotKey;
        private ListBox? lstDevices;
        private ComboBox? cmbSwitchDevice;
        private ComboBox? cmbDefaultDevice;
        private ComboBox? cmbThirdDevice;
        private CheckBox? chkStartWithWindows;
        private CheckBox? chkShowNotifications;
        private Button? BtnRefreshDevices;
        private Button? BtnTestSwitch;
        private Button? BtnSave;
        private Button? BtnCancel;
    }
}

#nullable restore
