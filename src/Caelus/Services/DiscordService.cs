using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Caelus.Core;

namespace Caelus.Services;

public sealed class DiscordService : IDisposable
{
    private NamedPipeClientStream? _pipe;
    private int _nonce;

    public bool Connected => _pipe is { IsConnected: true };

    public bool Connect(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return false;

        for (var i = 0; i < 10; i++)
        {
            try
            {
                var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
                pipe.Connect(200);
                _pipe = pipe;
                Send(0, JsonSerializer.Serialize(new { v = 1, client_id = clientId }));
                Logger.Write("Discord", $"Connected via discord-ipc-{i}");
                return true;
            }
            catch
            {
                _pipe?.Dispose();
                _pipe = null;
            }
        }

        Logger.Write("Discord", "Discord IPC is not available.");
        return false;
    }

    public void SetPresence(string details, string? state = null, string? placeId = null)
    {
        if (_pipe is null || !_pipe.IsConnected)
            return;

        var activity = new Dictionary<string, object?>
        {
            ["details"] = details,
            ["state"] = state ?? "Octane",
            ["timestamps"] = new Dictionary<string, object>
            {
                ["start"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            },
            ["assets"] = new Dictionary<string, object>
            {
                ["large_text"] = AppInfo.Name
            }
        };

        if (!string.IsNullOrWhiteSpace(placeId))
        {
            activity["buttons"] = new object[]
            {
                new Dictionary<string, string>
                {
                    ["label"] = "View on Octane",
                    // Unverified path - octane.wtf's real game-URL route wasn't in the binary strings.
                    ["url"] = $"https://octane.wtf/games/{placeId}"
                }
            };
        }

        var payload = new Dictionary<string, object>
        {
            ["cmd"] = "SET_ACTIVITY",
            ["nonce"] = Interlocked.Increment(ref _nonce).ToString(),
            ["args"] = new Dictionary<string, object>
            {
                ["pid"] = Environment.ProcessId,
                ["activity"] = activity
            }
        };

        Send(1, JsonSerializer.Serialize(payload));
    }

    public void Clear()
    {
        if (_pipe is null || !_pipe.IsConnected)
            return;

        var payload = new Dictionary<string, object>
        {
            ["cmd"] = "SET_ACTIVITY",
            ["nonce"] = Interlocked.Increment(ref _nonce).ToString(),
            ["args"] = new Dictionary<string, object>
            {
                ["pid"] = Environment.ProcessId
            }
        };

        Send(1, JsonSerializer.Serialize(payload));
    }

    private void Send(int opcode, string json)
    {
        if (_pipe is null)
            return;

        var data = Encoding.UTF8.GetBytes(json);
        using var frame = new MemoryStream();
        frame.Write(BitConverter.GetBytes(opcode));
        frame.Write(BitConverter.GetBytes(data.Length));
        frame.Write(data);
        _pipe.Write(frame.ToArray());
        _pipe.Flush();
    }

    public void Dispose()
    {
        try { Clear(); } catch { /* ignore */ }
        _pipe?.Dispose();
    }
}
