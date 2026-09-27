using System.Diagnostics;
using System.Windows.Threading;
using Caelus.Core;

namespace Caelus.Services;

/// <summary>
/// Tracks a running Octane client after launch. While a session is active the process stays alive
/// in the background (for Discord presence and integrations) and exits when the client closes.
/// </summary>
public sealed class GameSession : IDisposable
{
    private readonly DispatcherTimer _poll;
    private bool _ended;

    public Process Process { get; }
    public string? PlaceId { get; }
    public bool IsStudio { get; }

    public event Action? Ended;

    public GameSession(Process process, string? placeId, bool isStudio)
    {
        Process = process;
        PlaceId = placeId;
        IsStudio = isStudio;

        try
        {
            Process.EnableRaisingEvents = true;
            Process.Exited += (_, _) => Application.Current?.Dispatcher.BeginInvoke(End);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Logger.Write("Session", $"Cannot hook the client's exit event ({ex.Message}); polling instead.");
        }

        // Fallback in case the Exited event cannot be raised for this process.
        _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _poll.Tick += (_, _) =>
        {
            if (HasExited())
                End();
        };
        _poll.Start();
    }

    private bool HasExited()
    {
        try
        {
            return Process.HasExited;
        }
        catch
        {
            try
            {
                using var again = Process.GetProcessById(Process.Id);
                return again.HasExited;
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private void End()
    {
        if (_ended)
            return;

        _ended = true;
        _poll.Stop();
        Logger.Write("Session", "The Octane client closed.");
        Ended?.Invoke();
    }

    public void Dispose()
    {
        _poll.Stop();
        try
        {
            Process.Dispose();
        }
        catch
        {
            /* ignore */
        }
    }
}
