using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using RadioButton = System.Windows.Controls.RadioButton;
using ContentControl = System.Windows.Controls.ContentControl;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
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

    /// <summary>Set for the last step, which checks that closing the menu ends the process by itself.</summary>
    public static bool AllowExit { get; private set; }

    private static string _dir = "";
    private static AppTheme _theme;
    private static UiStyle _style;

    // In-memory only (saving is off in this mode), so the Appearance page shows the right selection.
    private static void UseTheme(AppTheme theme, UiStyle style)
    {
        App.Settings.Prop.Theme = theme;
        App.Settings.Prop.UiStyle = style;
        ThemeService.Apply(theme, style);
    }

    public static async Task RunAsync(string dir)
    {
        Active = true;
        App.SuppressSave = true;
        _theme = App.Settings.Prop.Theme;
        _style = App.Settings.Prop.UiStyle;
        _dir = dir;
        Directory.CreateDirectory(dir);
        try
        {
            var theme = _theme;
            await CaptureAllAsync(theme, UiStyle.Modern, "-modern");
            await CaptureAllAsync(theme, UiStyle.Classic, "-classic");
            if (theme != AppTheme.Dark)
                await CaptureAllAsync(AppTheme.Dark, UiStyle.Modern, "-modern-midnight");
            await CaptureThemesAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("UiShots", ex);
            File.WriteAllText(Path.Combine(dir, "error.txt"), ex.ToString());
        }
        finally
        {
            await CheckMenuExitAsync(dir);
        }
    }

    /// <summary>
    /// <c>-uitoggle &lt;folder&gt; [-realmouse]</c>: with the menu open on the Appearance page, flips the
    /// Classic style toggle back and forth for every theme and checks the result after each flip (style
    /// dictionary, palette, settings, toggle state, templates, page rebuilt). Flips 1-4 go through the
    /// automation peer; 5-12 are pointer clicks that aim once and then keep clicking the same spot
    /// (the switch/box, then the label), the way a person clicks a toggle twice. With -realmouse the
    /// xyxydark and classic themes also get genuine OS clicks (this moves the cursor briefly).
    /// Ends with a persistence step: <c>-persist Modern|Classic</c> saves that style through the toggle
    /// and checks Settings.json. Writes results.txt and a screenshot per flip.
    /// </summary>
    public static async Task RunToggleTestAsync(string dir, bool realMouse, UiStyle? persist)
    {
        Active = true;
        App.SuppressSave = true;
        _dir = dir;
        _theme = App.Settings.Prop.Theme;
        _style = App.Settings.Prop.UiStyle;
        Directory.CreateDirectory(dir);
        var log = new List<string> { $"start: saved theme={_theme} style={_style}, applied style={ThemeService.Style} ({MergedStyle()})" };
        var failures = 0;
        try
        {
            var menu = new MenuWindow();
            Offscreen(menu);
            menu.Show();
            await Settle(900);
            var host = (ContentControl)menu.FindName("PageHost");
            ((RadioButton)menu.FindName("NavAppearance")).IsChecked = true;
            await Settle(900);

            var themes = new[] { AppTheme.XyxyDark, AppTheme.Classic }
                .Concat(Enum.GetValues<AppTheme>().Where(t => t is not AppTheme.XyxyDark and not AppTheme.Classic));
            foreach (var theme in themes)
            {
                // Pick the theme (what its tile does) and scroll a little, like someone who scrolled to the toggle.
                App.Settings.Prop.Theme = theme;
                ThemeService.Apply(theme);
                await Settle(500);
                Scroller(host)?.ScrollToVerticalOffset(80);
                await Settle(300);
                SaveWindow(menu, $"{Name(theme)}-0-start-{Lower(ThemeService.Style)}.png");

                var last = theme is AppTheme.XyxyDark or AppTheme.Classic && realMouse ? 16 : 12;
                Point? spot = null;
                POINT? screenSpot = null;
                for (var flip = 1; flip <= last; flip++)
                {
                    var page = host.Content as Pages.AppearancePage;
                    var box = page?.FindName("ClassicStyleBox") as System.Windows.Controls.CheckBox;
                    if (box is null)
                    {
                        log.Add($"FAIL {theme} flip {flip}: Appearance page or toggle missing");
                        failures++;
                        break;
                    }

                    var expected = box.IsChecked == true ? UiStyle.Modern : UiStyle.Classic;
                    string how;
                    if (flip <= 4)
                    {
                        how = "automation toggle";
                        ((System.Windows.Automation.Provider.IToggleProvider)new System.Windows.Automation.Peers.CheckBoxAutomationPeer(box)).Toggle();
                    }
                    else if (flip <= 12)
                    {
                        // Aim at the switch (flip 5) or the label (flip 9) once, then keep clicking that spot.
                        if (flip is 5 or 9)
                            spot = AimPoint(box, onLabel: flip == 9, menu);
                        how = PointerClick(menu, box, spot!.Value, out var reached);
                        if (!reached)
                        {
                            log.Add($"FAIL {theme} flip {flip} -> {expected}: {how}");
                            failures++;
                            continue;
                        }
                    }
                    else
                    {
                        if (flip == 13)
                            screenSpot = await ScreenAimAsync(menu, box);
                        how = await RealClickAsync(menu, screenSpot!.Value) + $" before: {Where(menu, host)}";
                        if (flip == last)
                        {
                            // Done with the real mouse: back off-screen so nothing else can click the test window.
                            menu.Topmost = false;
                            Offscreen(menu);
                        }
                    }

                    await Settle(900);
                    var problems = Verify(menu, host, page!, expected, theme);
                    // The next click lands where this one did: the rebuilt toggle has to still be under it.
                    if (spot is not null && flip is >= 5 and <= 12 && !Reaches(menu, spot.Value, out var under))
                        problems.Add($"toggle moved away from the pointer (spot now hits {under})");
                    var file = $"{Name(theme)}-{flip:00}-{Lower(expected)}.png";
                    SaveWindow(menu, file);
                    log.Add(problems.Count == 0
                        ? $"PASS {theme} flip {flip} -> {expected} [{how}] ({file})"
                        : $"FAIL {theme} flip {flip} -> {expected} [{how}]: {string.Join("; ", problems)} ({Where(menu, host)})");
                    failures += problems.Count == 0 ? 0 : 1;
                }
            }

            // The recolored Octane palette on every window that uses the theme.
            await CaptureOctaneAsync(menu);

            if (persist is not null)
                failures += await PersistAsync(host, persist.Value, log);

            menu.Close();
        }
        catch (Exception ex)
        {
            log.Add("EXCEPTION " + ex);
            failures++;
        }
        finally
        {
            log.Add(failures == 0 ? "RESULT: all checks passed" : $"RESULT: {failures} failure(s)");
            File.WriteAllLines(Path.Combine(dir, "results.txt"), log);
            Application.Current.Shutdown();
        }
    }

    /// <summary>Saves <paramref name="style"/> through the toggle (with saving on) and checks Settings.json.</summary>
    private static async Task<int> PersistAsync(ContentControl host, UiStyle style, List<string> log)
    {
        App.Settings.Prop.Theme = _theme;
        ThemeService.Apply(_theme);
        await Settle(400);
        AllowSave = true;
        try
        {
            // Always goes through at least one real save: flip away first if the style is already the target.
            for (var i = 0; i < 2; i++)
            {
                if (host.Content is not Pages.AppearancePage page || page.FindName("ClassicStyleBox") is not System.Windows.Controls.CheckBox box)
                    break;
                if (i == 1 && (box.IsChecked == true) == (style == UiStyle.Classic))
                    break;
                ((System.Windows.Automation.Provider.IToggleProvider)new System.Windows.Automation.Peers.CheckBoxAutomationPeer(box)).Toggle();
                await Settle(900);
            }
        }
        finally
        {
            AllowSave = false;
        }

        var json = File.ReadAllText(Paths.Settings);
        // Only UiStyle is checked: the open menu may change the theme in the same file meanwhile.
        var ok = json.Contains($"\"UiStyle\": \"{style}\"") && ThemeService.Style == style;
        log.Add(ok
            ? $"PASS persist: Settings.json has UiStyle={style}"
            : $"FAIL persist: wanted UiStyle={style}; applied={ThemeService.Style}; file: {json.ReplaceLineEndings(" ")}");
        return ok ? 0 : 1;
    }

    private static async Task CaptureOctaneAsync(MenuWindow menu)
    {
        UseTheme(AppTheme.Octane, UiStyle.Modern);
        foreach (var name in new[] { "NavMods", "NavAppearance" })
        {
            ((RadioButton)menu.FindName(name)).IsChecked = true;
            await Settle(900);
            SaveWindow(menu, $"octane-modern-menu-{name[3..].ToLowerInvariant()}.png");
        }

        await ShowAndSave(new BootstrapperWindow(new LaunchArgs { Mode = LaunchMode.Player }), "octane-modern-launch.png");
        await ShowAndSave(new InstallerWindow(), "octane-modern-installer.png");
        await ShowAndSave(UpdateWindow.CreatePreview("2.2.1"), "octane-modern-update.png");
        foreach (var window in Application.Current.Windows.OfType<Window>().Where(w => w is not MenuWindow).ToList())
            window.Close();
        await Settle(300);
    }

    /// <summary>Lets <see cref="PersistAsync"/> (and the developer tests) write the settings file.</summary>
    public static bool AllowSave { get; internal set; }

    /// <summary>Developer tests: allow Discord connections, background mode, and link warnings for a non-installed copy.</summary>
    public static bool AllowDiscord { get; internal set; }
    public static bool AllowBackground { get; internal set; }
    public static bool CheckLinksAnyway { get; internal set; }

    /// <summary>Developer tests reuse this mode's guards (no saving by default, no stray exits).</summary>
    internal static void BeginDev(string dir)
    {
        Active = true;
        _dir = dir;
        Directory.CreateDirectory(dir);
    }

    internal static void SetAllowExit(bool allow) => AllowExit = allow;

    internal static Task SettleAsync(int ms) => Settle(ms);

    internal static void SaveWindowAs(Window window, string file) => SaveWindow(window, file);

    internal static void SaveElementAs(FrameworkElement element, string file, double pad = 16) => SaveElement(element, file, pad);

    internal static void PlaceOffscreen(Window window) => Offscreen(window);

    private static string Where(Window window, ContentControl host)
    {
        var scroller = Scroller(host);
        var box = (host.Content as FrameworkElement)?.FindName("ClassicStyleBox") as FrameworkElement;
        var center = box?.TranslatePoint(new Point(box.ActualWidth / 2, box.ActualHeight / 2), window);
        var focused = System.Windows.Input.Keyboard.FocusedElement as FrameworkElement;
        return $"toggle center={center?.X:0},{center?.Y:0} size={box?.ActualWidth:0}x{box?.ActualHeight:0}, offset={scroller?.VerticalOffset:0}/{scroller?.ScrollableHeight:0}, focus={focused?.GetType().Name}#{focused?.Name}";
    }

    private static string MergedStyle() =>
        string.Join(", ", Application.Current.Resources.MergedDictionaries.Select(d => d.Source is null ? "palette" : Path.GetFileName(d.Source.OriginalString)));

    private static ScrollViewer? Scroller(DependencyObject start)
    {
        for (var current = start; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is ScrollViewer viewer)
                return viewer;
        return null;
    }

    private static FrameworkElement HitPart(System.Windows.Controls.CheckBox box, bool onLabel)
    {
        box.ApplyTemplate();
        if (onLabel)
            return FindChild<ContentPresenter>(box) ?? (FrameworkElement)box;
        // Modern: the switch track; Classic: the square box.
        return (box.Template?.FindName("Track", box) ?? box.Template?.FindName("Box", box)) as FrameworkElement ?? box;
    }

    private static Point AimPoint(System.Windows.Controls.CheckBox box, bool onLabel, Window window)
    {
        var part = HitPart(box, onLabel);
        return part.TranslatePoint(new Point(part.ActualWidth / 2, part.ActualHeight / 2), window);
    }

    private static bool Reaches(Window window, Point point, out string chain)
    {
        var names = new List<string>();
        for (var current = window.InputHitTest(point) as DependencyObject; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            names.Add(current is FrameworkElement { Name.Length: > 0 } named ? $"{current.GetType().Name}#{named.Name}" : current.GetType().Name);
            if (current is System.Windows.Controls.CheckBox { Name: "ClassicStyleBox" })
            {
                chain = string.Join(" > ", names);
                return true;
            }
            if (names.Count >= 6)
                break;
        }

        chain = names.Count == 0 ? "nothing" : string.Join(" > ", names);
        return false;
    }

    /// <summary>
    /// A mouse click at a window point: hit-tests like Windows would, and only if that lands on the toggle
    /// runs what a click does there (the menu's press handler, focus, ButtonBase.OnClick).
    /// </summary>
    private static string PointerClick(Window window, System.Windows.Controls.CheckBox box, Point point, out bool reached)
    {
        reached = Reaches(window, point, out var chain);
        var where = $"click at {point.X:0},{point.Y:0} hits {chain}";
        if (!reached)
            return where + " (the toggle is not under the pointer, nothing happens)";

        var press = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent
        };
        ((window.InputHitTest(point) as UIElement) ?? box).RaiseEvent(press);
        box.Focus();
        typeof(System.Windows.Controls.Primitives.ButtonBase)
            .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(box, null);
        return where;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern void mouse_event(int flags, int dx, int dy, int data, IntPtr extra);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT p);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, int flags);
    private struct POINT { public int X; public int Y; }

    /// <summary>Brings the menu on screen (topmost) and returns the toggle switch's screen point.</summary>
    private static async Task<POINT> ScreenAimAsync(Window window, System.Windows.Controls.CheckBox box)
    {
        if (window.Left < -1000)
        {
            window.Topmost = true;
            window.Left = 40;
            window.Top = 40;
            window.Activate();
            await Settle(700);
        }

        var part = HitPart(box, onLabel: false);
        var screen = part.PointToScreen(new Point(part.ActualWidth / 2, part.ActualHeight / 2));
        return new POINT { X = (int)screen.X, Y = (int)screen.Y };
    }

    /// <summary>A genuine OS click at a fixed screen point; skipped if another window covers it.</summary>
    private static async Task<string> RealClickAsync(Window window, POINT pt)
    {
        var hwnd = ((System.Windows.Interop.HwndSource)PresentationSource.FromVisual(window)).Handle;
        if (GetAncestor(WindowFromPoint(pt), 2) != hwnd)
            return $"real click SKIPPED: another window covers {pt.X},{pt.Y}";

        GetCursorPos(out var saved);
        SetCursorPos(pt.X, pt.Y);
        await Settle(80);
        mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);
        await Settle(90);
        mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);
        await Settle(80);
        SetCursorPos(saved.X, saved.Y);
        return $"real OS click at screen {pt.X},{pt.Y}";
    }

    private static List<string> Verify(MenuWindow menu, ContentControl host, Pages.AppearancePage oldPage, UiStyle expected, AppTheme theme)
    {
        var problems = new List<string>();
        if (ThemeService.Style != expected) problems.Add($"ThemeService.Style={ThemeService.Style}");
        if (App.Settings.Prop.UiStyle != expected) problems.Add($"Settings.UiStyle={App.Settings.Prop.UiStyle}");
        if (App.Settings.Prop.Theme != theme) problems.Add($"Settings.Theme={App.Settings.Prop.Theme}");
        if (ThemeService.Current.Id != theme) problems.Add($"palette={ThemeService.Current.Id}");

        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count != 2)
            problems.Add($"merged count={merged.Count}");
        else if (merged[0].Source?.OriginalString.EndsWith($"{expected}.xaml", StringComparison.OrdinalIgnoreCase) != true)
            problems.Add($"merged[0]={merged[0].Source}");

        if (host.Content is not Pages.AppearancePage page)
        {
            problems.Add("page is not Appearance");
            return problems;
        }

        if (ReferenceEquals(page, oldPage)) problems.Add("page was not rebuilt");
        var box = page.FindName("ClassicStyleBox") as System.Windows.Controls.CheckBox;
        if (box is null)
        {
            problems.Add("toggle missing");
            return problems;
        }

        if ((box.IsChecked == true) != (expected == UiStyle.Classic)) problems.Add($"toggle IsChecked={box.IsChecked}");
        if (!box.IsLoaded) problems.Add("toggle not loaded");
        box.ApplyTemplate();
        // Modern templates have a switch track and nav icons; Classic ones have a check box and a pip.
        var track = box.Template?.FindName("Track", box);
        if ((track is not null) != (expected == UiStyle.Modern)) problems.Add($"toggle template wrong (Track={(track is not null)})");
        var nav = (RadioButton)menu.FindName("NavAppearance");
        var icon = nav.Template?.FindName("Icon", nav);
        if ((icon is not null) != (expected == UiStyle.Modern)) problems.Add($"nav template wrong (Icon={(icon is not null)})");
        return problems;
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            if (FindChild<T>(child) is { } nested) return nested;
        }
        return null;
    }

    private static string Name(AppTheme theme) => theme.ToString().ToLowerInvariant();

    private static string Lower(UiStyle style) => style.ToString().ToLowerInvariant();

    private static async Task CheckMenuExitAsync(string dir)
    {
        AllowExit = true;
        try
        {
            var menu = new MenuWindow();
            Offscreen(menu);
            menu.Show();
            await Settle(800);
            menu.Close();
        }
        catch (Exception ex)
        {
            Logger.Error("UiShots", ex);
        }

        // Closing the last window should shut the app down on its own; this only runs if it did not.
        await Task.Delay(5000);
        File.WriteAllText(Path.Combine(dir, "exit.txt"), "Closing the menu did not end the process; forced shutdown.");
        Application.Current.Shutdown();
    }

    private static readonly string[] NavNames =
        { "NavMods", "NavFlags", "NavAppearance", "NavBehaviour", "NavIntegrations", "NavInstall", "NavAbout" };

    private static async Task CaptureAllAsync(AppTheme theme, UiStyle style, string suffix)
    {
        UseTheme(theme, style);
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
        await ShowAndSave(UpdateWindow.CreatePreview("2.2.1"), $"update{suffix}.png");

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
        foreach (var style in new[] { UiStyle.Modern, UiStyle.Classic })
        {
            foreach (var id in Enum.GetValues<AppTheme>())
            {
                UseTheme(id, style);
                ((RadioButton)menu.FindName("NavAbout")).IsChecked = true;
                await Settle(300);
                ((RadioButton)menu.FindName("NavMods")).IsChecked = true;
                await Settle(900);
                var name = $"theme-{id.ToString().ToLowerInvariant()}-{style.ToString().ToLowerInvariant()}";
                SaveWindow(menu, $"{name}-menu.png");
                ((RadioButton)menu.FindName("NavAppearance")).IsChecked = true;
                await Settle(900);
                SaveWindow(menu, $"{name}-appearance.png");
            }
        }

        menu.Close();
        UseTheme(_theme, _style);
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
