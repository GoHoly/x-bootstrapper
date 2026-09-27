using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Caelus.Core;

namespace Caelus.Services;

/// <summary>
/// Notification-area icon shown while X Bootstrapper waits in the background for a game to close,
/// so the process is never invisible: Open settings, Open logs, Exit.
/// </summary>
internal static class TrayService
{
    private static NotifyIcon? _icon;

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
                    Icon = LoadIcon(),
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

    private static Icon LoadIcon()
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
}
