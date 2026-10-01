using System;
using System.Diagnostics;
using System.IO;

namespace SelectAI.Core.Utils;

public static class AppLog
{
    private static readonly object _lock = new();
    private static readonly string _logDir;
    private static readonly string _logFile;

    static AppLog()
    {
        try
        {
            _logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SelectAI");

            if (!Directory.Exists(_logDir))
            {
                Directory.CreateDirectory(_logDir);
            }

            _logFile = Path.Combine(_logDir, "app.log");
        }
        catch
        {
            _logDir = AppDomain.CurrentDomain.BaseDirectory;
            _logFile = Path.Combine(_logDir, "app.log");
        }
    }

    public static string LogFilePath => _logFile;

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
    {
        string details = ex != null ? $"{message} | Ex: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}" : message;
        Write("ERROR", details);
    }

    private static void Write(string level, string message)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        Trace.WriteLine(line);

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logFile, line + Environment.NewLine);
            }
            catch
            {
                // Fail-safe: Never throw from logger
            }
        }
    }
}
