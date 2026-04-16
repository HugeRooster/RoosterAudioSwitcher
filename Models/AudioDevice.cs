using System;

namespace RoosterAudioSwitcher.Models
{
    /// <summary>
    /// Represents an audio playback device detected on the system.
    /// </summary>
    public class AudioDevice
    {
        /// <summary>
        /// Unique identifier for the device (e.g., NAudio device ID).
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Friendly display name of the device.
        /// </summary>
        public string FriendlyName { get; set; } = string.Empty;

        /// <summary>
        /// Whether this device is currently the Windows default playback device.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// Whether the device is currently available/connected.
        /// </summary>
        public bool IsAvailable { get; set; } = true;

        /// <summary>
        /// Optional: Device type (e.g., "Speakers", "Headphones", "HDMI", "Bluetooth").
        /// </summary>
        public string? DeviceType { get; set; }

        public override string ToString() => IsDefault ? $"{FriendlyName} (Default)" : FriendlyName;

        public override bool Equals(object? obj) =>
            obj is AudioDevice device && device.Id == Id;

        public override int GetHashCode() =>
            Id.GetHashCode();
    }
}
