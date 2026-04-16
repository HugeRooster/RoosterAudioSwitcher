using System;
using System.IO;

namespace RoosterAudioSwitcher.Managers
{
    /// <summary>
    /// Simple file-based logger for debugging.
    /// </summary>
    public static class Logger
    {
        private static readonly string _logPath;
        private static readonly object _lockObj = new object();

        static Logger()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appConfigDir = Path.Combine(appDataPath, "RoosterAudioSwitcher");
            _logPath = Path.Combine(appConfigDir, "debug.log");
        }

        public static void Log(string message)
        {
            try
            {
                lock (_lockObj)
                {
                    string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                    File.AppendAllText(_logPath, logMessage + Environment.NewLine);
                    
                    // Also write to console for development
                    Console.WriteLine(logMessage);
                }
            }
            catch
            {
                // Silently fail if logging fails
            }
        }

        public static void LogError(string message, Exception? ex = null)
        {
            string fullMessage = ex != null 
                ? $"ERROR: {message}\n{ex.Message}\n{ex.StackTrace}"
                : $"ERROR: {message}";
            Log(fullMessage);
        }

        public static void ClearLog()
        {
            try
            {
                if (File.Exists(_logPath))
                {
                    File.Delete(_logPath);
                }
            }
            catch { }
        }
    }
}
