using System.IO;

namespace Caelus.Core;

public static class Logger
{
    private static readonly object Gate = new();
    private static StreamWriter? _writer;

    public static string? FilePath { get; private set; }

    public static void Initialize()
    {
        // Several instances can start in the same second (menu + a website launch), so the name
        // carries milliseconds and the process id, and a failure to open the log never stops the app.
        var name = $"x-bootstrapper_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}_{Environment.ProcessId}.log";
        foreach (var folder in new[] { Paths.Logs, Path.Combine(Path.GetTempPath(), "X Bootstrapper Logs") })
        {
            try
            {
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, name);
                _writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                    AutoFlush = true
                };
                FilePath = path;
                break;
            }
            catch (Exception ex)
            {
                DebugWrite($"Logger could not open {folder}: {ex.Message}");
            }
        }

        Write("Logger", $"{AppInfo.Name} {AppInfo.Version} starting");
        Write("Logger", $"Install: {Paths.Base}");
    }

    public static void Write(string source, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{source}] {message}";
        lock (Gate)
        {
            try
            {
                _writer?.WriteLine(line);
            }
            catch
            {
                /* disk full / file gone: keep running */
            }

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
