using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Caelus.Core;

namespace Caelus.Services;

/// <summary>
/// Minimal Discord Rich Presence client over the local IPC pipe. Uses the built-in X Bootstrapper
/// application ID (AppInfo.DiscordClientId) unless a custom one is set.
/// Presence is only sent after Discord answers the handshake with READY.
/// </summary>
public sealed class DiscordService : IDisposable
{
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(3);

    private readonly object _writeGate = new();
    private readonly CancellationTokenSource _stop = new();
    private NamedPipeClientStream? _pipe;
    private int _nonce;
    private bool _disposed;

    public bool Connected => _pipe is { IsConnected: true } && !_disposed;

    /// <summary>Connects and waits for READY. Returns null if Discord isn't running or rejects the ID.</summary>
    public static async Task<DiscordService?> ConnectAsync(string? clientId, CancellationToken token = default)
    {
        clientId = clientId?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            Logger.Write("Discord", "Rich Presence is on but no application ID is set; skipping.");
            return null;
        }

        if (!clientId.All(char.IsDigit))
        {
            Logger.Write("Discord", "The Discord application ID should be a number; skipping.");
            return null;
        }

        for (var i = 0; i < 10; i++)
        {
            NamedPipeClientStream? pipe = null;
            try
            {
                pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(200, token);
            }
            catch
            {
                pipe?.Dispose();
                continue;
            }

            var service = new DiscordService { _pipe = pipe };
            try
            {
                service.Send(0, JsonSerializer.Serialize(new { v = 1, client_id = clientId }));
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(ReadyTimeout);
                if (await service.WaitForReadyAsync(timeout.Token))
                {
                    Logger.Write("Discord", $"Connected via discord-ipc-{i}");
                    _ = service.DrainAsync();
                    return service;
                }
            }
            catch (Exception ex)
            {
                Logger.Write("Discord", $"Handshake on discord-ipc-{i} failed: {ex.Message}");
            }

            service.Dispose();
            return null;
        }

        Logger.Write("Discord", "Discord is not running (no IPC pipe).");
        return null;
    }

    private async Task<bool> WaitForReadyAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var (opcode, json) = await ReadFrameAsync(token);
            if (opcode == 2)
            {
                Logger.Write("Discord", $"Discord closed the connection: {json}");
                return false;
            }

            if (opcode != 1)
                continue;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("evt", out var evt))
            {
                var name = evt.GetString();
                if (name == "READY")
                    return true;
                if (name == "ERROR")
                {
                    Logger.Write("Discord", $"Discord rejected the handshake: {json}");
                    return false;
                }
            }
        }

        return false;
    }

    // Discord answers every command; read (and log errors from) replies so the pipe never backs up.
    private async Task DrainAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested && _pipe is { IsConnected: true })
            {
                var (opcode, json) = await ReadFrameAsync(_stop.Token);
                if (opcode == 2)
                {
                    Logger.Write("Discord", "Discord closed the connection.");
                    return;
                }

                if (json.Contains("\"evt\":\"ERROR\"", StringComparison.Ordinal))
                    Logger.Write("Discord", $"Discord error: {json}");
            }
        }
        catch
        {
            /* pipe closed */
        }
    }

    private async Task<(int Opcode, string Json)> ReadFrameAsync(CancellationToken token)
    {
        var pipe = _pipe ?? throw new IOException("Not connected.");
        var header = new byte[8];
        await pipe.ReadExactlyAsync(header, token);
        var opcode = BitConverter.ToInt32(header, 0);
        var length = BitConverter.ToInt32(header, 4);
        if (length < 0 || length > 1 << 20)
            throw new IOException("Invalid frame from Discord.");

        var body = new byte[length];
        await pipe.ReadExactlyAsync(body, token);
        return (opcode, Encoding.UTF8.GetString(body));
    }

    public void SetPresence(string details, string? state, DateTimeOffset started)
    {
        var activity = new Dictionary<string, object?>
        {
            ["details"] = details,
            ["timestamps"] = new Dictionary<string, object> { ["start"] = started.ToUnixTimeSeconds() },
            ["assets"] = new Dictionary<string, object>
            {
                ["large_image"] = AppInfo.DiscordLargeImage,
                ["large_text"] = AppInfo.DiscordLargeText
            },
            ["instance"] = false
        };
        if (!string.IsNullOrWhiteSpace(state))
            activity["state"] = state;

        SendActivity(activity);
    }

    public void Clear() => SendActivity(null);

    private void SendActivity(Dictionary<string, object?>? activity)
    {
        if (!Connected)
            return;

        var args = new Dictionary<string, object?> { ["pid"] = Environment.ProcessId };
        if (activity is not null)
            args["activity"] = activity;

        var payload = new Dictionary<string, object>
        {
            ["cmd"] = "SET_ACTIVITY",
            ["nonce"] = Interlocked.Increment(ref _nonce).ToString(),
            ["args"] = args
        };

        try
        {
            Send(1, JsonSerializer.Serialize(payload));
        }
        catch (Exception ex)
        {
            Logger.Write("Discord", $"Could not update presence: {ex.Message}");
        }
    }

    private void Send(int opcode, string json)
    {
        var pipe = _pipe;
        if (pipe is null)
            return;

        var data = Encoding.UTF8.GetBytes(json);
        var frame = new byte[8 + data.Length];
        BitConverter.GetBytes(opcode).CopyTo(frame, 0);
        BitConverter.GetBytes(data.Length).CopyTo(frame, 4);
        data.CopyTo(frame, 8);
        lock (_writeGate)
        {
            pipe.Write(frame, 0, frame.Length);
            pipe.Flush();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try { Clear(); } catch { /* ignore */ }
        _disposed = true;
        _stop.Cancel();
        _pipe?.Dispose();
        _pipe = null;
        _stop.Dispose();
    }
}
