using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using RoosterAudioSwitcher.Models;

namespace RoosterAudioSwitcher.Managers
{
    /// <summary>
    /// Manages audio device enumeration, switching, and change detection using NAudio Core Audio API.
    /// </summary>
    public class AudioDeviceManager : IDisposable
    {
        private MMDeviceEnumerator? _deviceEnumerator;
        private List<AudioDevice> _devices;

        /// <summary>
        /// Fired when the list of audio devices changes (device added/removed/enabled/disabled).
        /// </summary>
        public event EventHandler<EventArgs>? DevicesChanged;

        /// <summary>
        /// Fired when the default playback device is changed.
        /// </summary>
        public event EventHandler<AudioDeviceChangedEventArgs>? DefaultDeviceChanged;

        public AudioDeviceManager()
        {
            _devices = new List<AudioDevice>();
            Logger.Log("AudioDeviceManager: Initializing...");
            InitializeDeviceEnumerator();
            Logger.Log($"AudioDeviceManager: Found {_devices.Count} audio devices");
        }

        /// <summary>
    /// Initializes the MMDeviceEnumerator and sets up for device change notifications.
        /// <summary>
        /// Initializes the audio device enumerator.
        /// </summary>
        private void InitializeDeviceEnumerator()
        {
            try
            {
                _deviceEnumerator = new MMDeviceEnumerator();

                // Note: We use a polling approach rather than IMMNotificationClient
                // because device change notifications require COM event handling which is complex.
                // The app can call RefreshDeviceList() periodically or on user interaction.
                // Load initial device list
                Logger.Log("InitializeDeviceEnumerator: MMDeviceEnumerator created successfully");
                RefreshDeviceList();
            }
            catch (Exception ex)
            {
                Logger.LogError($"InitializeDeviceEnumerator: Error initializing device enumerator", ex);
            }
        }

        /// <summary>
        /// Refreshes the list of available audio playback devices from the system.
        /// </summary>
        public void RefreshDeviceList()
        {
            try
            {
                Logger.Log("RefreshDeviceList: Refreshing device list...");
                
                if (_deviceEnumerator == null)
                {
                    Logger.LogError("RefreshDeviceList: Device enumerator is null");
                    return;
                }

                var oldDevices = _devices.ToList();
                _devices.Clear();

                // Get all active playback devices
                var enumerator = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                foreach (var device in enumerator)
                {
                    try
                    {
                        var audioDevice = new AudioDevice
                        {
                            Id = device.ID,
                            FriendlyName = device.FriendlyName,
                            IsAvailable = true,
                            IsDefault = device.ID == GetDefaultDeviceId()
                        };

                        Logger.Log($"RefreshDeviceList: Found device: {audioDevice.FriendlyName} (ID: {audioDevice.Id}, Default: {audioDevice.IsDefault})");
                        _devices.Add(audioDevice);
                    }
                    catch (Exception exDevice)
                    {
                        Logger.LogError($"RefreshDeviceList: Error adding device", exDevice);
                    }
                }

                Logger.Log($"RefreshDeviceList: Total devices found: {_devices.Count}");

                // Check if device list changed
                if (!DeviceListsEqual(oldDevices, _devices))
                {
                    Logger.Log("RefreshDeviceList: Device list changed, firing DevicesChanged event");
                    DevicesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"RefreshDeviceList: Error refreshing device list", ex);
            }
        }

        /// <summary>
        /// Gets the current list of available audio devices.
        /// </summary>
        public List<AudioDevice> GetDevices() => new List<AudioDevice>(_devices);

        /// <summary>
        /// Sets the specified device as the Windows default playback device.
        /// </summary>
        public bool SetDefaultDevice(AudioDevice device)
        {
            try
            {
                Logger.Log($"SetDefaultDevice: Attempting to set device: {device.FriendlyName} (ID: {device.Id})");
                
                if (_deviceEnumerator == null)
                {
                    Logger.LogError("SetDefaultDevice: Device enumerator is null");
                    return false;
                }

                // Get the actual device from the enumerator
                var mmDevice = _deviceEnumerator.GetDevice(device.Id);
                if (mmDevice == null)
                {
                    Logger.LogError($"SetDefaultDevice: Device {device.Id} not found in enumerator");
                    return false;
                }

                Logger.Log($"SetDefaultDevice: Found device in enumerator, attempting COM interop...");
                
                // Try to set as default using COM interop
                bool success = AudioDeviceHelper.SetDefaultDevice(device.Id);
                if (!success)
                {
                    Logger.LogError($"SetDefaultDevice: Failed to set device {device.FriendlyName} as default via COM");
                    return false;
                }

                // Small delay to allow Windows to process the change
                System.Threading.Thread.Sleep(100);

                // Update device list to reflect new default
                RefreshDeviceList();

                var newDefault = _devices.FirstOrDefault(d => d.Id == device.Id);
                if (newDefault != null)
                {
                    DefaultDeviceChanged?.Invoke(this, new AudioDeviceChangedEventArgs(newDefault));
                    Logger.Log($"✓ SetDefaultDevice: Successfully switched to device: {newDefault.FriendlyName}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"SetDefaultDevice: Error", ex);
                return false;
            }
        }

        /// <summary>
        /// Cycles to the next device in the list and sets it as default.
        /// </summary>
        public AudioDevice? CycleToNextDevice()
        {
            Logger.Log("CycleToNextDevice: Called");
            Logger.Log($"CycleToNextDevice: Total devices: {_devices.Count}");
            
            if (_devices.Count == 0)
            {
                Logger.LogError("CycleToNextDevice: No devices available");
                return null;
            }

            var currentDefault = _devices.FirstOrDefault(d => d.IsDefault);
            if (currentDefault == null)
            {
                // No current default, use first device
                Logger.Log("CycleToNextDevice: No current default device, using first device");
                return SetDefaultDevice(_devices[0]) ? _devices[0] : null;
            }

            Logger.Log($"CycleToNextDevice: Current default: {currentDefault.FriendlyName}");
            
            int currentIndex = _devices.IndexOf(currentDefault);
            int nextIndex = (currentIndex + 1) % _devices.Count;

            var nextDevice = _devices[nextIndex];
            Logger.Log($"CycleToNextDevice: Next device index: {nextIndex}, device: {nextDevice.FriendlyName}");
            return SetDefaultDevice(nextDevice) ? nextDevice : null;
        }

        /// <summary>
        /// Gets the device ID of the current Windows default playback device.
        /// </summary>
        private string GetDefaultDeviceId()
        {
            try
            {
                var defaultDevice = _deviceEnumerator?.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return defaultDevice?.ID ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Sets the default playback device using the Windows Policy Config interface (COM).
        /// This is more reliable than using NAudio's device methods for setting defaults.
        /// </summary>
        private void SetDefaultDeviceViaPolicy(string deviceId)
        {
            try
            {
                AudioDeviceHelper.SetDefaultDevice(deviceId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting default via policy: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper to compare device lists for equality.
        /// </summary>
        private static bool DeviceListsEqual(List<AudioDevice> list1, List<AudioDevice> list2)
        {
            if (list1.Count != list2.Count)
                return false;

            return list1.SequenceEqual(list2, new AudioDeviceIdComparer());
        }

        public void Dispose()
        {
            _deviceEnumerator?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Comparer for AudioDevice objects based on ID only.
    /// </summary>
    internal class AudioDeviceIdComparer : IEqualityComparer<AudioDevice>
    {
        public bool Equals(AudioDevice? x, AudioDevice? y) =>
            x?.Id == y?.Id;

        public int GetHashCode(AudioDevice obj) =>
            obj.Id.GetHashCode();
    }

    /// <summary>
    /// Event args for default device change events.
    /// </summary>
    public class AudioDeviceChangedEventArgs : EventArgs
    {
        public AudioDevice Device { get; }

        public AudioDeviceChangedEventArgs(AudioDevice device)
        {
            Device = device;
        }
    }

    /// <summary>
    /// Helper class to set the default audio device using COM interop.
    /// </summary>
    internal static class AudioDeviceHelper
    {
        private const uint CLSCTX_INPROC_SERVER = 0x1;
        private static readonly Guid CLSID_PolicyConfigClient = new("870af99c-171d-4f9e-af0d-e63df40c2bc9");
        private static readonly Guid IID_IPolicyConfig = new("f8679f50-850a-41cf-9c72-430f290290c8");
        private static readonly Guid CLSID_PolicyConfigVistaClient = new("294935ce-f637-4e7c-a41b-ab255460b862");
        private static readonly Guid IID_IPolicyConfigVista = new("568b9108-44bf-40b4-9006-86afe5b5a620");

        private enum ERole
        {
            eConsole = 0,
            eMultimedia = 1,
            eCommunications = 2
        }

        [DllImport("ole32.dll")]
        private static extern int CoCreateInstance(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
            IntPtr pUnkOuter,
            uint dwClsContext,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            out IntPtr ppv);

        [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig
        {
            [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, IntPtr ppFormat);
            [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int bDefault, IntPtr ppFormat);
            [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, IntPtr pEndpointFormat, IntPtr pMixFormat);
            [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
            [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, IntPtr pmftPeriod);
            [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, IntPtr pMode);
            [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, IntPtr pMode);
            [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, IntPtr key, IntPtr pv);
            [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, IntPtr key, IntPtr pv);
            [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, ERole role);
            [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int bVisible);
        }

        [ComImport, Guid("568b9108-44bf-40b4-9006-86afe5b5a620"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfigVista
        {
            [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, IntPtr ppFormat);
            [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int bDefault, IntPtr ppFormat);
            [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, IntPtr pEndpointFormat, IntPtr pMixFormat);
            [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
            [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, IntPtr pmftPeriod);
            [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, IntPtr pMode);
            [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, IntPtr pMode);
            [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, IntPtr key, IntPtr pv);
            [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, IntPtr key, IntPtr pv);
            [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, ERole role);
            [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int bVisible);
        }

        public static bool SetDefaultDevice(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                Logger.LogError("SetDefaultDevice: Device ID is empty");
                return false;
            }

            try
            {
                Logger.Log($"SetDefaultDevice: Attempting COM default endpoint switch for device {deviceId}");

                if (!TrySetDefaultDeviceViaCOM(deviceId))
                {
                    Logger.LogError("SetDefaultDevice: COM switch call failed");
                    return false;
                }

                if (!VerifyDefaultEndpoint(deviceId))
                {
                    Logger.LogError("SetDefaultDevice: COM call returned success but default endpoint did not change");
                    return false;
                }

                Logger.Log("SetDefaultDevice: Verified endpoint change");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("SetDefaultDevice: Error", ex);
                return false;
            }
        }

        private static bool TrySetDefaultDeviceViaCOM(string deviceId)
        {
            bool modernOk = TrySetDefaultDeviceViaModernPolicyConfig(deviceId);
            if (modernOk)
            {
                return true;
            }

            Logger.Log("TrySetDefaultDeviceViaCOM: Falling back to Vista policy config interface");
            return TrySetDefaultDeviceViaVistaPolicyConfig(deviceId);
        }

        private static bool TrySetDefaultDeviceViaModernPolicyConfig(string deviceId)
        {
            IntPtr policyConfigPtr = IntPtr.Zero;

            try
            {
                int createHr = CoCreateInstance(CLSID_PolicyConfigClient, IntPtr.Zero, CLSCTX_INPROC_SERVER, IID_IPolicyConfig, out policyConfigPtr);
                if (createHr != 0)
                {
                    Logger.Log($"TrySetDefaultDeviceViaModernPolicyConfig: CoCreateInstance failed 0x{createHr:X8}");
                    return false;
                }

                var policyConfig = Marshal.GetObjectForIUnknown(policyConfigPtr) as IPolicyConfig;
                if (policyConfig == null)
                {
                    Logger.LogError("TrySetDefaultDeviceViaModernPolicyConfig: Failed to marshal IPolicyConfig");
                    return false;
                }

                int hrMultimedia = policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
                int hrConsole = policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole);
                int hrComm = policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications);

                Logger.Log($"TrySetDefaultDeviceViaModernPolicyConfig: SetDefaultEndpoint results multimedia=0x{hrMultimedia:X8}, console=0x{hrConsole:X8}, comm=0x{hrComm:X8}");

                return hrMultimedia == 0;
            }
            catch (Exception ex)
            {
                Logger.LogError("TrySetDefaultDeviceViaModernPolicyConfig: Exception", ex);
                return false;
            }
            finally
            {
                if (policyConfigPtr != IntPtr.Zero)
                {
                    Marshal.Release(policyConfigPtr);
                }
            }
        }

        private static bool TrySetDefaultDeviceViaVistaPolicyConfig(string deviceId)
        {
            IntPtr policyConfigPtr = IntPtr.Zero;

            try
            {
                int createHr = CoCreateInstance(CLSID_PolicyConfigVistaClient, IntPtr.Zero, CLSCTX_INPROC_SERVER, IID_IPolicyConfigVista, out policyConfigPtr);
                if (createHr != 0)
                {
                    Logger.Log($"TrySetDefaultDeviceViaVistaPolicyConfig: CoCreateInstance failed 0x{createHr:X8}");
                    return false;
                }

                var policyConfig = Marshal.GetObjectForIUnknown(policyConfigPtr) as IPolicyConfigVista;
                if (policyConfig == null)
                {
                    Logger.LogError("TrySetDefaultDeviceViaVistaPolicyConfig: Failed to marshal IPolicyConfigVista");
                    return false;
                }

                int hrMultimedia = policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
                int hrConsole = policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole);
                int hrComm = policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications);

                Logger.Log($"TrySetDefaultDeviceViaVistaPolicyConfig: SetDefaultEndpoint results multimedia=0x{hrMultimedia:X8}, console=0x{hrConsole:X8}, comm=0x{hrComm:X8}");

                return hrMultimedia == 0;
            }
            catch (Exception ex)
            {
                Logger.LogError("TrySetDefaultDeviceViaVistaPolicyConfig: Exception", ex);
                return false;
            }
            finally
            {
                if (policyConfigPtr != IntPtr.Zero)
                {
                    Marshal.Release(policyConfigPtr);
                }
            }
        }

        private static bool VerifyDefaultEndpoint(string expectedDeviceId)
        {
            const int maxAttempts = 5;

            for (int i = 1; i <= maxAttempts; i++)
            {
                try
                {
                    using var enumerator = new MMDeviceEnumerator();
                    string current = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;

                    Logger.Log($"VerifyDefaultEndpoint: attempt {i}/{maxAttempts}, current={current}");
                    if (string.Equals(current, expectedDeviceId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"VerifyDefaultEndpoint: attempt {i} failed", ex);
                }

                System.Threading.Thread.Sleep(120);
            }

            return false;
        }
    }
}
