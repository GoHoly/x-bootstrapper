using System.Windows.Media;
using Caelus.Core;

namespace Caelus.Services;

/// <summary>Plays a single mods preview sound at a time (mp3/wav; ogg when Windows can decode it).</summary>
public static class ModAudioPreview
{
    private static readonly object Gate = new();
    private static MediaPlayer? _player;
    private static string? _playingPath;

    public static bool IsPlaying(string? path)
    {
        lock (Gate)
            return _player is not null && path is not null &&
                   string.Equals(_playingPath, path, StringComparison.OrdinalIgnoreCase);
    }

    public static void Toggle(string path)
    {
        lock (Gate)
        {
            if (_player is not null &&
                string.Equals(_playingPath, path, StringComparison.OrdinalIgnoreCase))
            {
                StopUnlocked();
                return;
            }

            StopUnlocked();
            try
            {
                var player = new MediaPlayer();
                player.MediaEnded += (_, _) =>
                {
                    lock (Gate)
                    {
                        if (ReferenceEquals(_player, player))
                            StopUnlocked();
                    }
                };
                player.MediaFailed += (_, args) =>
                {
                    Logger.Write("ModPreview", $"Could not play {path}: {args.ErrorException?.Message}");
                    lock (Gate)
                    {
                        if (ReferenceEquals(_player, player))
                            StopUnlocked();
                    }
                };
                player.Open(new Uri(path));
                player.Volume = 0.85;
                player.Play();
                _player = player;
                _playingPath = path;
            }
            catch (Exception ex)
            {
                Logger.Write("ModPreview", $"Play failed: {ex.Message}");
                StopUnlocked();
            }
        }
    }

    public static void Stop()
    {
        lock (Gate)
            StopUnlocked();
    }

    private static void StopUnlocked()
    {
        try
        {
            _player?.Stop();
            _player?.Close();
        }
        catch
        {
            /* ignore */
        }

        _player = null;
        _playingPath = null;
    }
}
