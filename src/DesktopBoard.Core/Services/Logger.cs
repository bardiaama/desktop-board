using System.Text;

namespace DesktopBoard.Core.Services;

/// <summary>
/// Minimal append-only file logger. Deliberately not a logging framework: the app has
/// no server, and one readable text file next to the database is what users can send us.
/// </summary>
public static class Logger
{
    private static readonly object Gate = new();
    private static string? _path;

    public static string? Path => _path;

    public static void Initialize(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = System.IO.Path.Combine(directory, "desktopboard.log");
        try
        {
            if (File.Exists(_path) && new FileInfo(_path).Length > 2 * 1024 * 1024)
                File.Move(_path, System.IO.Path.Combine(directory, "desktopboard.previous.log"), overwrite: true);
        }
        catch { /* ignore rotation failures */ }
        Info("---- session start ----");
    }

    public static void Info(string message) => Write("INFO ", message);
    public static void Warn(string message) => Write("WARN ", message);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", ex is null ? message : message + Environment.NewLine + ex);

    private static void Write(string level, string message)
    {
        if (_path is null) return;
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}";
        lock (Gate)
        {
            try { File.AppendAllText(_path, line, Encoding.UTF8); } catch { /* never throw from logging */ }
        }
    }
}
