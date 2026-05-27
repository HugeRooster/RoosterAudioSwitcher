using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RoosterAudioSwitcher.Managers
{
    /// <summary>
    /// Manages global hotkey registration and handling using Windows P/Invoke.
    /// </summary>
    public partial class HotkeyManager : IDisposable
    {
        // Win32 P/Invoke declarations
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_ALT = 0x0001;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_WIN = 0x0008;
        private const int HOTKEY_ID_SWITCH = 9000;
        private const int HOTKEY_ID_RETURN = 9001;
        private const int HOTKEY_ID_THIRD = 9002;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern short GetAsyncKeyState(int vKey);

        private HotkeyMessageWindow? _messageWindow;
        private string? _switchHotKeyString;
        private string? _returnHotKeyString;
        private string? _thirdHotKeyString;

        /// <summary>
        /// Fired when the registered global hotkey is pressed.
        /// </summary>
        public event EventHandler? SwitchHotKeyPressed;
        public event EventHandler? ReturnHotKeyPressed;
        public event EventHandler? ThirdHotKeyPressed;

        public HotkeyManager()
        {
            try
            {
                Logger.Log("Initializing HotkeyManager...");
                // Create a hidden window to receive hotkey messages
                _messageWindow = new HotkeyMessageWindow(this);
                Logger.Log("HotkeyMessageWindow created successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error initializing HotkeyManager", ex);
            }
        }

        /// <summary>
        /// Registers a global hotkey. Must be called from the main UI thread.
        /// Format examples: "Ctrl+Alt+S", "Windows+A", "Ctrl+Shift+D"
        /// </summary>
        public bool RegisterSwitchHotKey(string hotKeyString)
        {
            return RegisterHotKeyInternal(hotKeyString, HOTKEY_ID_SWITCH, "switch", value => _switchHotKeyString = value);
        }

        public bool RegisterReturnHotKey(string hotKeyString)
        {
            return RegisterHotKeyInternal(hotKeyString, HOTKEY_ID_RETURN, "return", value => _returnHotKeyString = value);
        }

        public bool RegisterThirdHotKey(string hotKeyString)
        {
            return RegisterHotKeyInternal(hotKeyString, HOTKEY_ID_THIRD, "third", value => _thirdHotKeyString = value);
        }

        private bool RegisterHotKeyInternal(string hotKeyString, int hotkeyId, string label, Action<string?> setCurrent)
        {
            try
            {
                Logger.Log($"RegisterHotKey ({label}) called with: {hotKeyString}");
                
                // Unregister old hotkey if it exists
                UnregisterHotKeyById(hotkeyId);

                if (_messageWindow == null)
                {
                    Logger.LogError("Message window is null");
                    return false;
                }

                if (_messageWindow.Handle == IntPtr.Zero)
                {
                    Logger.LogError("Message window handle is zero");
                    return false;
                }

                // Parse the hotkey string (e.g., "Ctrl+Alt+S")
                var parts = hotKeyString.Split('+');
                uint modifiers = 0;
                uint vk = 0;

                foreach (var part in parts)
                {
                    string p = part.Trim().ToLower();
                    Logger.Log($"  Parsing key part: {p}");
                    switch (p)
                    {
                        case "ctrl":
                            modifiers |= MOD_CONTROL;
                            break;
                        case "alt":
                            modifiers |= MOD_ALT;
                            break;
                        case "shift":
                            modifiers |= MOD_SHIFT;
                            break;
                        case "windows":
                        case "win":
                            modifiers |= MOD_WIN;
                            break;
                        default:
                            // Try to parse as a Windows Forms key
                            if (Enum.TryParse<Keys>(part.Trim(), true, out var parsedKey))
                            {
                                vk = (uint)parsedKey;
                                Logger.Log($"  Parsed key: {parsedKey} (VK={vk})");
                            }
                            break;
                    }
                }

                if (vk == 0)
                {
                    Logger.LogError($"Invalid hotkey string: {hotKeyString}");
                    return false;
                }

                Logger.Log($"  Modifiers: 0x{modifiers:X}, VK: 0x{vk:X}");
                Logger.Log($"  Window Handle: {_messageWindow.Handle}");

                // Register the hotkey
                bool result = RegisterHotKey(_messageWindow.Handle, hotkeyId, modifiers, vk);
                if (!result)
                {
                    Logger.LogError($"Failed to register {label} hotkey. It may be in use by another application. RegisterHotKey returned false");
                    return false;
                }

                setCurrent(hotKeyString);
                Logger.Log($"✓ {label} hotkey registered successfully: {hotKeyString}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error registering {label} hotkey", ex);
                return false;
            }
        }

        /// <summary>
        /// Legacy wrapper retained for compatibility with existing callers.
        /// </summary>
        public bool RegisterHotKey(string hotKeyString) => RegisterSwitchHotKey(hotKeyString);

        /// <summary>
        /// Unregisters all registered global hotkeys.
        /// </summary>
        public void UnregisterAllHotkeys()
        {
            UnregisterHotKeyById(HOTKEY_ID_SWITCH);
            UnregisterHotKeyById(HOTKEY_ID_RETURN);
            UnregisterHotKeyById(HOTKEY_ID_THIRD);
            _switchHotKeyString = null;
            _returnHotKeyString = null;
            _thirdHotKeyString = null;
        }

        private void UnregisterHotKeyById(int hotkeyId)
        {
            try
            {
                var window = _messageWindow;
                if (window is not null && window.Handle != IntPtr.Zero)
                {
                    UnregisterHotKey(window.Handle, hotkeyId);
                    Logger.Log($"Hotkey {hotkeyId} unregistered successfully");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error unregistering hotkey", ex);
            }
        }

        /// <summary>
        /// Gets the currently registered hotkey string, or null if none registered.
        /// </summary>
        public string? GetSwitchHotKeyString() => _switchHotKeyString;
        public string? GetReturnHotKeyString() => _returnHotKeyString;
        public string? GetThirdHotKeyString() => _thirdHotKeyString;

        /// <summary>
        /// Internal method called when the hotkey is pressed.
        /// </summary>
        internal void OnHotKeyPressed(int hotkeyId)
        {
            Logger.Log($">>> HOTKEY PRESSED <<< id={hotkeyId}");
            if (hotkeyId == HOTKEY_ID_SWITCH)
            {
                SwitchHotKeyPressed?.Invoke(this, EventArgs.Empty);
            }
            else if (hotkeyId == HOTKEY_ID_RETURN)
            {
                ReturnHotKeyPressed?.Invoke(this, EventArgs.Empty);
            }
            else if (hotkeyId == HOTKEY_ID_THIRD)
            {
                ThirdHotKeyPressed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            UnregisterAllHotkeys();
            _messageWindow?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Hidden message window to receive Windows messages for hotkey events.
    /// </summary>
    internal class HotkeyMessageWindow : Form
    {
        private readonly HotkeyManager _hotkeyManager;
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID_SWITCH = 9000;
        private const int HOTKEY_ID_RETURN = 9001;
        private const int HOTKEY_ID_THIRD = 9002;

        public HotkeyMessageWindow(HotkeyManager hotkeyManager)
        {
            _hotkeyManager = hotkeyManager;
            ShowInTaskbar = false;
            Visible = false;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Minimized;
            Text = "RoosterAudioSwitcher_HotkeyWindow";
            
            Logger.Log("HotkeyMessageWindow created");
            Logger.Log($"  Handle: {this.Handle}");
            Logger.Log($"  Visible: {this.Visible}");
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                Logger.Log($"WndProc: Received WM_HOTKEY message (wParam={m.WParam})");
                if (m.WParam.ToInt32() == HOTKEY_ID_SWITCH || m.WParam.ToInt32() == HOTKEY_ID_RETURN || m.WParam.ToInt32() == HOTKEY_ID_THIRD)
                {
                    Logger.Log("WndProc: HOTKEY_ID matched! Calling OnHotKeyPressed");
                    _hotkeyManager.OnHotKeyPressed(m.WParam.ToInt32());
                }
            }
            base.WndProc(ref m);
        }
    }
}
