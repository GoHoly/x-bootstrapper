using System.Drawing;
using System.Windows.Forms;
using System.Xml.Linq;
using Caelus.Core;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Caelus.Services;

internal static class NotifyService
{
    private static NotifyIcon? _icon;
    private static readonly object Gate = new();

    public static void Show(string title, string body)
    {
        try
        {
            var app = System.Windows.Application.Current;
            if (app?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => ShowCore(title, body));
                return;
            }

            ShowCore(title, body);
        }
        catch (Exception ex)
        {
            Logger.Write("Notify", ex.Message);
        }
    }

    private static void ShowCore(string title, string body)
    {
        if (TryShowToast(title, body))
            return;

        ShowBalloon(title, body);
    }

    private static bool TryShowToast(string title, string body)
    {
        try
        {
            var payload = new XElement("toast",
                new XElement("visual",
                    new XElement("binding",
                        new XAttribute("template", "ToastGeneric"),
                        new XElement("text", title),
                        new XElement("text", body))));

            var xml = new XmlDocument();
            xml.LoadXml(payload.ToString(SaveOptions.DisableFormatting));
            var toast = new ToastNotification(xml)
            {
                ExpirationTime = DateTimeOffset.Now.AddSeconds(12)
            };
            ToastNotificationManager.CreateToastNotifier(AppInfo.AppUserModelId).Show(toast);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Write("Notify", ex.Message);
            return false;
        }
    }

    private static void ShowBalloon(string title, string body)
    {
        lock (Gate)
        {
            EnsureIcon();
            if (_icon is null)
                return;

            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = body;
            _icon.BalloonTipIcon = ToolTipIcon.Info;
            _icon.ShowBalloonTip(6000);
        }
    }

    private static void EnsureIcon()
    {
        if (_icon is not null)
            return;

        Icon? icon = null;
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
                icon = Icon.ExtractAssociatedIcon(path);
        }
        catch
        {
            /* fall back below */
        }

        _icon = new NotifyIcon
        {
            Icon = icon ?? SystemIcons.Application,
            Visible = true,
            Text = AppInfo.Name
        };
    }

    public static void Dispose()
    {
        lock (Gate)
        {
            if (_icon is null)
                return;
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }
}
