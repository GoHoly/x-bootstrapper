using System.Media;
using Caelus.Models;

namespace Caelus.UI;

internal static class UiSound
{
    private const int Rate = 44100;
    private const float Master = 0.38f;
    private static readonly Dictionary<AppTheme, byte[]> Clicks = new();
    private static readonly Dictionary<AppTheme, byte[]> Themes = new();
    private static readonly object Gate = new();
    private static long _lastClick;

    public static bool Enabled =>
        App.Settings?.Prop.UiSounds != false;

    public static void PlayClick()
    {
        if (!Enabled)
            return;

        var now = Environment.TickCount64;
        lock (Gate)
        {
            if (now - _lastClick < 50)
                return;
            _lastClick = now;
        }

        Play(ClickWav(ThemeService.Current.Id));
    }

    public static void PlayTheme()
    {
        if (!Enabled)
            return;

        Play(ThemeWav(ThemeService.Current.Id));
    }

    private static void Play(byte[] wav)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                using var stream = new MemoryStream(wav, writable: false);
                using var player = new SoundPlayer(stream);
                player.PlaySync();
            }
            catch
            {
                /* sound is optional */
            }
        });
    }

    private static byte[] ClickWav(AppTheme theme)
    {
        lock (Clicks)
        {
            if (Clicks.TryGetValue(theme, out var cached))
                return cached;
            var wav = Pack(BuildClick(theme));
            Clicks[theme] = wav;
            return wav;
        }
    }

    private static byte[] ThemeWav(AppTheme theme)
    {
        lock (Themes)
        {
            if (Themes.TryGetValue(theme, out var cached))
                return cached;
            var wav = Pack(BuildTheme(theme));
            Themes[theme] = wav;
            return wav;
        }
    }

    private static float[] BuildClick(AppTheme theme) => theme switch
    {
        AppTheme.Halloween => Tick(196, 0.07, 0.32, 0.11),
        AppTheme.Xyxy => Keycap(night: false, rich: false),
        AppTheme.XyxyDark => Keycap(night: true, rich: false),
        AppTheme.Dark => Tick(168, 0.06, 0.20, 0.10),
        AppTheme.Octane => Tick(214, 0.065, 0.26, 0.10),
        AppTheme.Dusk => Tick(154, 0.08, 0.14, 0.09),
        AppTheme.Light => Tick(430, 0.045, 0.62, 0.08),
        AppTheme.Classic => Tick(784, 0.035, 0.85, 0.07),
        _ => Tick(220, 0.06, 0.3, 0.09)
    };

    private static float[] BuildTheme(AppTheme theme)
    {
        if (theme is AppTheme.Xyxy)
            return Keycap(night: false, rich: true);
        if (theme is AppTheme.XyxyDark)
            return Keycap(night: true, rich: true);
        if (theme is AppTheme.Halloween)
            return Mix(Tick(180, 0.08, 0.28, 0.12), Tick(92, 0.12, 0.18, 0.09), 0.05);

        var click = BuildClick(theme);
        var extra = Tick(theme switch
        {
            AppTheme.Classic => 523,
            AppTheme.Light => 620,
            AppTheme.Octane => 320,
            _ => 250
        }, 0.09, 0.35, 0.07);

        return Mix(click, extra, 0.04);
    }

    private static float[] Keycap(bool night, bool rich)
    {
        var seconds = rich ? 0.16 : 0.09;
        var n = (int)(Rate * seconds);
        var samples = new float[n];
        var rng = new Random(night ? 11 : 5);
        double phaseLo = 0, phaseMid = 0, phaseHi = 0;
        double lp = 0, noiseLp = 0, prevNoise = 0, noiseHp = 0;
        var fLo = night ? 118.0 : 142.0;
        var fMid = night ? 198.0 : 236.0;
        var fHi = night ? 640.0 : 760.0;
        var gain = night ? 0.72 : 0.78;

        for (var i = 0; i < n; i++)
        {
            var t = i / (double)Rate;
            var drop = Math.Exp(-t * 9.5);
            phaseLo += Math.Tau * fLo * drop / Rate;
            phaseMid += Math.Tau * fMid * drop / Rate;
            phaseHi += Math.Tau * fHi / Rate;

            var down = SmoothAttack(t, 0.0016) * Math.Exp(-t * (rich ? 20 : 34));
            var noiseEnv = SmoothAttack(t, 0.0008) * Math.Exp(-t * 78);
            var plastic = SmoothAttack(t, 0.001) * Math.Exp(-t * 110);

            var noise = rng.NextDouble() * 2 - 1;
            noiseHp = 0.55 * (noiseHp + noise - prevNoise);
            prevNoise = noise;
            noiseLp += 0.18 * (noiseHp - noiseLp);

            var up = 0d;
            if (rich && t > 0.046)
            {
                var u = t - 0.046;
                up = SmoothAttack(u, 0.002) * Math.Exp(-u * 36) * 0.38;
            }

            var body = Math.Sin(phaseLo) * 0.52
                       + Math.Sin(phaseMid) * 0.40
                       + Math.Sin(phaseHi) * plastic * 0.06;
            var raw = (body * (down + up) + noiseLp * noiseEnv * 0.20) * gain;
            lp += 0.30 * (raw - lp);
            samples[i] = (float)lp;
        }

        return samples;
    }

    private static float[] Tick(double freq, double seconds, double brightness, double gain)
    {
        var n = (int)(Rate * seconds);
        var samples = new float[n];
        double phase = 0, lp = 0;
        var filter = 0.12 + brightness * 0.55;

        for (var i = 0; i < n; i++)
        {
            var t = i / (double)Rate;
            phase += Math.Tau * freq / Rate;
            var env = SmoothAttack(t, 0.004) * Math.Exp(-t * (18 + (1 - brightness) * 10));
            var raw = Math.Sin(phase) * env * gain;
            lp += filter * (raw - lp);
            samples[i] = (float)lp;
        }

        return samples;
    }

    private static float[] Mix(float[] a, float[] b, double delay)
    {
        var offset = (int)(delay * Rate);
        var n = Math.Max(a.Length, offset + b.Length);
        var dest = new float[n];
        Array.Copy(a, dest, a.Length);
        for (var i = 0; i < b.Length; i++)
            dest[offset + i] += b[i];
        return dest;
    }

    private static double SmoothAttack(double t, double attack) =>
        t >= attack ? 1 : 0.5 - 0.5 * Math.Cos(Math.PI * t / attack);

    private static byte[] Pack(float[] samples)
    {
        var data = samples.Length * 2;
        using var stream = new MemoryStream(44 + data);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + data);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(Rate);
        writer.Write(Rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(data);
        foreach (var sample in samples)
        {
            var clipped = Math.Clamp(sample * Master, -1f, 1f);
            writer.Write((short)(clipped * 32767));
        }

        return stream.ToArray();
    }
}
