using System;
using System.IO;

namespace RoosterAudioSwitcher.Managers
{
    /// <summary>
    /// Simple file-based logger for debugging with automatic log rotation.
    /// </summary>
    public static class Logger
    {
        private const int MaxLogFileSizeBytes = 5 * 1024 * 1024; // 5 MB
        private const int MaxBackupLogs = 3;

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

                    // Check if rotation is needed after append
                    RotateLogIfNecessary();
                }
            }
            catch
            {
                // Silently fail if logging fails
            }
        }

        public static void LogWarning(string message)
        {
            try
            {
                lock (_lockObj)
                {
                    string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] WARNING: {message}";
                    File.AppendAllText(_logPath, logMessage + Environment.NewLine);

                    // Also write to console for development visibility
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
            string fullMessage;
            if (ex != null)
            {
                // Truncate stack trace to prevent unbounded log growth and reduce info exposure.
                // Keep only the first 10 lines of the stack trace.
                var stackLines = ex.StackTrace?.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                string? truncatedStack = null;

                if (stackLines != null && stackLines.Length > 0)
                {
                    int maxLines = Math.Min(stackLines.Length, 10);
                    truncatedStack = string.Join("\n", stackLines[..maxLines]);
                    if (stackLines.Length > maxLines)
                    {
                        truncatedStack += $"\n... (truncated, {stackLines.Length - maxLines} more lines)";
                    }
                }

                fullMessage = ex != null
                    ? $"ERROR: {message}\n{ex.Message}{(truncatedStack != null ? $"\n{truncatedStack}" : "")}"
                    : $"ERROR: {message}";
            }
            else
            {
                fullMessage = $"ERROR: {message}";
            }

            Log(fullMessage);
        }

        /// <summary>
        /// Rotates the log file if it exceeds the maximum size.
        /// Keeps up to MaxBackupLogs rotated files (debug.log.1, debug.log.2, etc.).
        /// </summary>
        private static void RotateLogIfNecessary()
        {
            try
            {
                if (!File.Exists(_logPath))
                    return;

                var fileInfo = new FileInfo(_logPath);
                if (fileInfo.Length <= MaxLogFileSizeBytes)
                    return;

                // Shift existing backups: .3 -> delete, .2 -> .3, .1 -> .2
                for (int i = MaxBackupLogs; i > 0; i--)
                {
                    string backupPath = $"{_logPath}.{i}";
                    string previousPath = $"{_logPath}.{i - 1}";

                    if (i == MaxBackupLogs && File.Exists(backupPath))
                    {
                        // Delete the oldest backup to make room
                        File.Delete(backupPath);
                    }

                    if (i > 1 && File.Exists(previousPath))
                    {
                        File.Move(previousPath, backupPath);
                    }
                }

                // Move current log to .1 and create a fresh file
                string firstBackup = $"{_logPath}.1";
                if (File.Exists(firstBackup))
                {
                    File.Delete(firstBackup);
                }

                File.Move(_logPath, firstBackup);
            }
            catch
            {
                // Silently fail — logging failure should not break the app
            }
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
