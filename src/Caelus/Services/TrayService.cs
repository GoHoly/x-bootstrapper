using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using Caelus.Core;
using Caelus.Models;
using Caelus.UI;

namespace Caelus.Services;

/// <summary>
/// Notification-area icon shown while X Bootstrapper waits in the background for a game to close,
/// so the process is never invisible: Open settings, Open logs, Exit.
/// </summary>
internal static class TrayService
{
    private static NotifyIcon? _icon;
    private static Icon? _themeIcon;

    public static void Show(string status)
    {
        try
        {
            if (_icon is null)
            {
                var menu = new ContextMenuStrip();
                menu.Items.Add("Open settings", null, (_, _) => App.ShowMenu());
                menu.Items.Add("Open logs", null, (_, _) => OpenLogs());
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("Exit", null, (_, _) => App.ExitFromTray());

                _icon = new NotifyIcon
                {
                    Icon = LoadThemeIcon(),
                    ContextMenuStrip = menu
                };
                _icon.DoubleClick += (_, _) => App.ShowMenu();
            }

            // NotifyIcon text is limited to 127 characters.
            var text = $"{AppInfo.Name}: {status}";
            _icon.Text = text.Length > 127 ? text[..127] : text;
            _icon.Visible = true;
        }
        catch (Exception ex)
        {
            Logger.Write("Tray", $"Tray icon unavailable: {ex.Message}");
        }
    }

    /// <summary>Swap the tray glyph when Appearance changes theme (same logos as the in-app mark).</summary>
    public static void ApplyThemeIcon()
    {
        if (_icon is null)
            return;

        try
        {
            _icon.Icon = LoadThemeIcon();
        }
        catch (Exception ex)
        {
            Logger.Write("Tray", $"Could not refresh tray icon: {ex.Message}");
        }
    }

    public static void Hide()
    {
        if (_icon is null)
            return;

        try
        {
            _icon.Visible = false;
            _icon.ContextMenuStrip?.Dispose();
            _icon.Dispose();
        }
        catch
        {
            /* ignore */
        }

        _icon = null;
        DisposeThemeIcon();
    }

    private static void OpenLogs()
    {
        try
        {
            var target = Logger.FilePath is not null && File.Exists(Logger.FilePath)
                ? Path.GetDirectoryName(Logger.FilePath)!
                : Paths.Logs;
            Directory.CreateDirectory(target);
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Write("Tray", $"Could not open logs: {ex.Message}");
        }
    }

    private static Icon LoadThemeIcon()
    {
        DisposeThemeIcon();
        try
        {
            _themeIcon = CreateIconFromLogo(ThemeService.Current.Id);
            if (_themeIcon is not null)
                return _themeIcon;
        }
        catch
        {
            /* fall back to exe icon */
        }

        return LoadExeIcon();
    }

    private static void DisposeThemeIcon()
    {
        _themeIcon?.Dispose();
        _themeIcon = null;
    }

    private static Icon LoadExeIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
                return Icon.ExtractAssociatedIcon(path) ?? SystemIcons.Application;
        }
        catch
        {
            /* fall back */
        }

        return SystemIcons.Application;
    }

    /// <summary>Build a WinForms Icon from the theme's pack PNG so tray matches taskbar / in-app logo.</summary>
    private static Icon? CreateIconFromLogo(AppTheme theme)
    {
        var source = ThemeService.Logo(theme);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var png = new MemoryStream();
        encoder.Save(png);
        png.Position = 0;

        using var bitmap = new Bitmap(png);
        using var sized = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(sized))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(bitmap, 0, 0, 32, 32);
        }

        var handle = sized.GetHicon();
        try
        {
            // Clone so DestroyIcon can free the temporary HICON without invalidating our Icon.
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}
