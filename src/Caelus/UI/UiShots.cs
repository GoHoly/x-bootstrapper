using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using RadioButton = System.Windows.Controls.RadioButton;
using ContentControl = System.Windows.Controls.ContentControl;
using Brushes = System.Windows.Media.Brushes;
using Caelus.Core;
using Caelus.Models;

namespace Caelus.UI;

/// <summary>
/// Developer screenshot mode: <c>"X Bootstrapper.exe" -uishots &lt;folder&gt;</c> renders every window and
/// menu page to PNG (off-screen, nothing is launched, installed, or saved) and exits.
/// </summary>
internal static class UiShots
{
    public static bool Active { get; private set; }

    private static string _dir = "";

    public static async Task RunAsync(string dir)
    {
        Active = true;
        App.SuppressSave = true;
        _dir = dir;
        Directory.CreateDirectory(dir);
        try
        {
            await CaptureAllAsync(App.Settings.Prop.Theme, "");
            await CaptureThemesAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("UiShots", ex);
            File.WriteAllText(Path.Combine(dir, "error.txt"), ex.ToString());
        }
        finally
        {
            Application.Current.Shutdown();
        }
    }

    private static readonly string[] NavNames =
        { "NavMods", "NavFlags", "NavAppearance", "NavBehaviour", "NavIntegrations", "NavInstall", "NavAbout" };

    private static async Task CaptureAllAsync(AppTheme theme, string suffix)
    {
        ThemeService.Apply(theme);
        var menu = new MenuWindow();
        Offscreen(menu);
        menu.Show();
        await Settle(900);
        var host = (ContentControl)menu.FindName("PageHost");
        foreach (var name in NavNames)
        {
            var nav = (RadioButton)menu.FindName(name);
            nav.IsChecked = true;
            await Settle(name == "NavInstall" ? 4000 : 1000);
            var page = name[3..].ToLowerInvariant();
            SaveWindow(menu, $"menu-{page}{suffix}.png");
            if (host.Content is FrameworkElement content)
                SaveElement(content, $"menu-{page}-full{suffix}.png", 24);
        }

        var boot = new BootstrapperWindow(new LaunchArgs { Mode = LaunchMode.Player });
        await ShowAndSave(boot, $"launch{suffix}.png");
        await ShowAndSave(new InstallerWindow(), $"installer{suffix}.png");
        await ShowAndSave(new UninstallWindow(), $"uninstall{suffix}.png");
        await ShowAndSave(UpdateWindow.CreatePreview("2.2.0"), $"update{suffix}.png");

        foreach (Window window in Application.Current.Windows.OfType<Window>().ToList())
            window.Close();
        await Settle(300);
    }

    private static async Task CaptureThemesAsync()
    {
        var menu = new MenuWindow();
        Offscreen(menu);
        menu.Show();
        await Settle(900);
        foreach (var palette in ThemeService.All)
        {
            ThemeService.Apply(palette.Id);
            ((RadioButton)menu.FindName("NavAbout")).IsChecked = true;
            await Settle(200);
            ((RadioButton)menu.FindName("NavMods")).IsChecked = true;
            await Settle(900);
            SaveWindow(menu, $"theme-{palette.Id.ToString().ToLowerInvariant()}-menu.png");
        }

        menu.Close();
        ThemeService.Apply(App.Settings.Prop.Theme);
    }

    private static async Task ShowAndSave(Window window, string file)
    {
        Offscreen(window);
        window.Show();
        await Settle(900);
        SaveWindow(window, file);
    }

    private static void Offscreen(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -32000;
        window.Top = -32000;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
    }

    private static async Task Settle(int ms)
    {
        await Task.Delay(ms);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static void SaveWindow(Window window, string file)
    {
        if (window.Content is FrameworkElement root)
            Save(root, root.ActualWidth, root.ActualHeight, file, window.Background, 0);
    }

    private static void SaveElement(FrameworkElement element, string file, double pad)
    {
        var background = Application.Current.TryFindResource("BackgroundBrush") as Brush ?? Brushes.Black;
        Save(element, element.ActualWidth, element.ActualHeight, file, background, pad);
    }

    private static void Save(Visual visual, double width, double height, string file, Brush? background, double pad)
    {
        if (width < 1 || height < 1)
            return;

        const double scale = 1.5;
        var totalW = width + pad * 2;
        var totalH = height + pad * 2;
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            dc.DrawRectangle(background ?? Brushes.Black, null, new Rect(0, 0, totalW, totalH));
            dc.DrawRectangle(new VisualBrush(visual)
            {
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top
            }, null, new Rect(pad, pad, width, height));
        }

        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(totalW * scale), (int)Math.Ceiling(totalH * scale),
            96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(_dir, file));
        encoder.Save(stream);
    }
}
