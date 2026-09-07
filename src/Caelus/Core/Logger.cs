using System.IO;

namespace Caelus.Core;

public static class Logger
{
    private static readonly object Gate = new();
    private static StreamWriter? _writer;

    public static string? FilePath { get; private set; }

    public static void Initialize()
    {
        FilePath = Path.Combine(Paths.Logs, $"x-bootstrapper_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
        _writer = new StreamWriter(new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };

        Write("Logger", $"{AppInfo.Name} {AppInfo.Version} starting");
        Write("Logger", $"Install: {Paths.Base}");
    }

    public static void Write(string source, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{source}] {message}";
        lock (Gate)
        {
            _writer?.WriteLine(line);
            DebugWrite(line);
        }
    }

    public static void Error(string source, Exception ex)
    {
        Write(source, ex.ToString());
    }

    private static void DebugWrite(string line)
    {
        System.Diagnostics.Debug.WriteLine(line);
    }

    public static void Close()
    {
        lock (Gate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
