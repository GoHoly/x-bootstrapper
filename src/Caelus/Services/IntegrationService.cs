using System.Diagnostics;
using Caelus.Core;
using Caelus.Models;

namespace Caelus.Services;

/// <summary>
/// Starts your own programs (from Integrations) when a game session starts, and closes the ones
/// marked "close with the game" when it ends. Only processes started here are ever closed.
/// </summary>
public static class IntegrationService
{
    private static readonly List<Process> Started = new();
    private static readonly object Gate = new();

    public static void Start(IEnumerable<Integration>? integrations)
    {
        if (integrations is null)
            return;

        foreach (var item in integrations.Where(item => item.Enabled && !string.IsNullOrWhiteSpace(item.Path)))
        {
            var path = Environment.ExpandEnvironmentVariables(item.Path.Trim().Trim('"'));
            var label = string.IsNullOrWhiteSpace(item.Name) ? Path.GetFileName(path) : item.Name;
            try
            {
                if (!File.Exists(path))
                {
                    Logger.Write("Integrations", $"Skipped {label}: {path} does not exist.");
                    continue;
                }

                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = item.Arguments ?? "",
                    WorkingDirectory = Path.GetDirectoryName(path) ?? "",
                    UseShellExecute = true
                });

                Logger.Write("Integrations", $"Started {label}{(process is null ? "" : $" ({process.Id})")}");
                if (process is null)
                    continue;

                if (item.AutoClose)
                {
                    lock (Gate)
                        Started.Add(process);
                }
                else
                {
                    process.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Write("Integrations", $"Could not start {label}: {ex.Message}");
            }
        }
    }

    /// <summary>Closes programs started by <see cref="Start"/> that are set to close with the game.</summary>
    public static void StopAll()
    {
        List<Process> toClose;
        lock (Gate)
        {
            toClose = Started.ToList();
            Started.Clear();
        }

        foreach (var process in toClose)
        {
            try
            {
                if (process.HasExited)
                    continue;

                var name = process.ProcessName;
                // Ask nicely first so the program can save, then end it.
                if (!process.CloseMainWindow() || !process.WaitForExit(3000))
                    process.Kill();
                Logger.Write("Integrations", $"Closed {name} ({process.Id})");
            }
            catch (Exception ex)
            {
                Logger.Write("Integrations", $"Could not close process: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
