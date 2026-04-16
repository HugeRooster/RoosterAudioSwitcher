using System;
using System.Windows.Forms;

namespace RoosterAudioSwitcher.Managers
{
    /// <summary>
    /// Manages Windows system notifications using BalloonTip (tray) notifications.
    /// </summary>
    public class NotificationManager
    {
        private readonly TrayIconManager _trayIconManager;

        public NotificationManager(TrayIconManager trayIconManager)
        {
            _trayIconManager = trayIconManager;
        }

        /// <summary>
        /// Shows a notification when the audio device changes.
        /// </summary>
        public void ShowDeviceChangeNotification(string deviceName)
        {
            try
            {
                _trayIconManager.ShowBalloonTip(
                    "Audio Device Changed",
                    $"Now playing through: {deviceName}",
                    ToolTipIcon.Info,
                    3000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a simple notification.
        /// </summary>
        public void ShowNotification(string title, string message)
        {
            try
            {
                _trayIconManager.ShowBalloonTip(title, message, ToolTipIcon.Info, 3000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing notification: {ex.Message}");
            }
        }
    }
}
