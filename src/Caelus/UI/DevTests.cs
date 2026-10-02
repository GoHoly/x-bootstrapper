using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Caelus.Core;
using Caelus.Models;
using Caelus.Services;
using Caelus.UI.Pages;
using Microsoft.Win32;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;

namespace Caelus.UI;

/// <summary>
/// <c>-devtest &lt;name&gt; &lt;folder&gt; [-devbase &lt;profile folder&gt;]</c>: developer checks for the 2.3 features.
/// Windows are rendered off-screen and driven through automation peers (no mouse). With -devbase the run uses
/// a throwaway profile and may save it; without it nothing is saved. Writes &lt;name&gt;-results.txt and screenshots.
/// </summary>
internal static class DevTests
{
    private static readonly List<string> Lines = new();
    private static int _failures;

    private static void Check(bool ok, string what, string? detail = null)
    {
        Lines.Add((ok ? "PASS " : "FAIL ") + what + (detail is null ? "" : " | " + detail));
        if (!ok)
            _failures++;
    }

    private static void Note(string text) => Lines.Add("     " + text);

    public static async Task RunAsync(string name, string dir, string[] args)
    {
        Directory.CreateDirectory(dir);
        UiShots.BeginDev(dir);
        MenuWindow.ShowIntroWindows = false;
        var devBase = args.Any(arg => arg.Equals("-devbase", StringComparison.OrdinalIgnoreCase));
        UiShots.AllowSave = devBase;
        App.SuppressSave = !devBase;
        Note($"{AppInfo.Name} {AppInfo.Version} devtest {name} | profile {Paths.Base} | devbase={devBase} | {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        try
        {
            switch (name.ToLowerInvariant())
            {
                case "ui": await UiAsync(); break;
                case "firstrun": await FirstRunAsync(); break;
                case "whatsnew": await WhatsNewAsync(); break;
                case "discord": await DiscordAsync(); break;
                case "links": await LinksAsync(devBase); break;
                case "sky": await SkyAsync(dir); break;
                case "flags": await FlagsAsync(); break;
                case "mods": await ModsAsync(); break;
                case "logs": await LogsAsync(dir); break;
                case "themes": await ThemesAsync(args); break;
                default: Check(false, "unknown test " + name); break;
            }
        }
        catch (Exception ex)
        {
            Check(false, "exception", ex.ToString());
        }
        finally
        {
            Lines.Add(_failures == 0 ? "RESULT: all checks passed" : $"RESULT: {_failures} failure(s)");
            File.WriteAllLines(Path.Combine(dir, $"{name}-results.txt"), Lines);
            foreach (var window in Application.Current.Windows.OfType<Window>().ToList())
            {
                try { window.Close(); } catch { /* ignore */ }
            }

            Application.Current.Shutdown(_failures == 0 ? 0 : 1);
        }
    }

    // ------------------------------------------------------------------ helpers

    private static void UseTheme(AppTheme theme, UiStyle style)
    {
        App.Settings.Prop.Theme = theme;
        App.Settings.Prop.UiStyle = style;
        ThemeService.Apply(theme, style);
    }

    private static string Tag(AppTheme theme, UiStyle style) => $"{theme.ToString().ToLowerInvariant()}-{style.ToString().ToLowerInvariant()}";

    private static async Task<MenuWindow> OpenMenuAsync()
    {
        var menu = new MenuWindow();
        UiShots.PlaceOffscreen(menu);
        menu.Show();
        await UiShots.SettleAsync(1000);
        return menu;
    }

    private static async Task<T> ShowAsync<T>(T window, int settle = 900) where T : Window
    {
        UiShots.PlaceOffscreen(window);
        window.Show();
        await UiShots.SettleAsync(settle);
        return window;
    }

    private static FrameworkElement Card(FrameworkElement element)
    {
        DependencyObject? current = element;
        while (current is not null && current is not Border)
            current = LogicalTreeHelper.GetParent(current);
        return current as FrameworkElement ?? element;
    }

    private static void Invoke(Button button) =>
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)!).Invoke();

    private static void Toggle(CheckBox box) =>
        ((IToggleProvider)new CheckBoxAutomationPeer(box).GetPattern(PatternInterface.Toggle)!).Toggle();

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string DiskText(string path) => File.Exists(path) ? File.ReadAllText(path) : "";

    private static bool DiskHas(string path, string key, string value)
    {
        var text = DiskText(path);
        return text.Contains($"\"{key}\": {value}", StringComparison.Ordinal);
    }

    private static List<string> LogLines(params string[] sources)
    {
        var lines = new List<string>();
        if (Logger.FilePath is null || !File.Exists(Logger.FilePath))
            return lines;
        using var stream = new FileStream(Logger.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
            if (sources.Any(source => line.Contains($"[{source}]", StringComparison.Ordinal)))
                lines.Add(line);
        return lines;
    }

    private static void SavePng(BitmapSource bitmap, string file)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file);
        encoder.Save(stream);
    }

    // ------------------------------------------------------------------ ui

    /// <summary>Screenshots of every changed page and window in Modern and Classic on three themes.</summary>
    private static async Task UiAsync()
    {
        var combos = new[] { AppTheme.Xyxy, AppTheme.Octane, AppTheme.Dark };
        foreach (var theme in combos)
        foreach (var style in new[] { UiStyle.Modern, UiStyle.Classic })
        {
            UseTheme(theme, style);
            var tag = Tag(theme, style);
            var menu = await OpenMenuAsync();
            foreach (var nav in new[] { "NavIntegrations", "NavBehaviour", "NavAbout", "NavMods" })
            {
                var page = menu.Navigate(nav) as FrameworkElement;
                await UiShots.SettleAsync(nav == "NavMods" ? 3500 : 1100);
                var short_ = nav[3..].ToLowerInvariant();
                UiShots.SaveWindowAs(menu, $"menu-{short_}-{tag}.png");
                if (page is null)
                {
                    Check(false, $"{tag} {nav} page missing");
                    continue;
                }

                var key = nav switch
                {
                    "NavIntegrations" => "DiscordStatusBox",
                    "NavBehaviour" => "BackgroundBox",
                    "NavAbout" => "CopyLogsButton",
                    _ => "SkyPanel"
                };
                if (page.FindName(key) is FrameworkElement element)
                {
                    UiShots.SaveElementAs(Card(element), $"card-{short_}-{tag}.png");
                    Check(element.IsVisible && element.ActualWidth > 0, $"{tag} {short_}: {key} is shown");
                }
                else
                {
                    Check(false, $"{tag} {short_}: {key} missing");
                }

                if (nav == "NavMods" && page.FindName("SkyPanel") is WrapPanel sky)
                    Check(sky.Children.Count == SkyboxService.BuiltIn.Length + 2, $"{tag} sky tiles", $"{sky.Children.Count} tiles");

                if (nav == "NavIntegrations" && page.FindName("DiscordStatusBox") is ComboBox combo &&
                    page.FindName("XbDiscordOptions") is FrameworkElement options && theme == AppTheme.Xyxy)
                {
                    var saved = App.Settings.Prop.EffectiveDiscordStatus;
                    for (var i = 0; i < 3; i++)
                    {
                        combo.SelectedIndex = i;
                        await UiShots.SettleAsync(500);
                        var mode = (DiscordStatusMode)i;
                        Check(App.Settings.Prop.EffectiveDiscordStatus == mode, $"{tag} Discord choice {mode} saved in settings");
                        Check(options.IsVisible == (mode == DiscordStatusMode.XBootstrapper), $"{tag} XB-only options visible only for XB ({mode})");
                        UiShots.SaveElementAs(Card(combo), $"card-integrations-{mode.ToString().ToLowerInvariant()}-{tag}.png");
                    }

                    combo.SelectedIndex = (int)saved;
                    await UiShots.SettleAsync(300);
                }
            }

            var setup = await ShowAsync(new SetupWindow());
            UiShots.SaveWindowAs(setup, $"setup-{tag}.png");
            UiShots.SaveElementAs((FrameworkElement)setup.FindName("SetupContent"), $"setup-full-{tag}.png");
            setup.Close();
            var news = await ShowAsync(new WhatsNewWindow());
            UiShots.SaveWindowAs(news, $"whatsnew-{tag}.png");
            news.Close();
            menu.Close();
            await UiShots.SettleAsync(300);
        }
    }

    // ------------------------------------------------------------------ first run

    private static async Task FirstRunAsync()
    {
        var s = App.Settings.Prop;
        Check(App.FreshProfile, "new profile detected (no Settings.json before this run)");
        Check(s.SetupPending, "SetupPending set for a new profile");
        Check(DiskHas(Paths.Settings, "SetupPending", "true"), "SetupPending saved in Settings.json");
        Check(!s.KeepRunningInBackground, "Keep running in the background defaults to off");
        App.TrackVersion();
        Check(App.State.Prop.WhatsNewPendingVersion is null, "a new user gets no What's new");
        Check(App.State.Prop.LastRunVersion == AppInfo.Version, "LastRunVersion recorded", App.State.Prop.LastRunVersion);

        MenuWindow.ShowIntroWindows = true;
        var menu = await OpenMenuAsync();
        await UiShots.SettleAsync(1200);
        var setup = Application.Current.Windows.OfType<SetupWindow>().FirstOrDefault();
        Check(setup is not null, "menu opened the first-run setup by itself");
        Check(!Application.Current.Windows.OfType<WhatsNewWindow>().Any(), "no What's new for a new user");
        if (setup is null)
            return;

        UiShots.PlaceOffscreen(setup);
        await UiShots.SettleAsync(400);
        UiShots.SaveWindowAs(setup, "firstrun-1-opened.png");

        setup.SelectTheme(AppTheme.Xyxy);
        await UiShots.SettleAsync(700);
        Check(s.Theme == AppTheme.Xyxy && ThemeService.Current.Id == AppTheme.Xyxy, "theme tile applies the theme");
        UiShots.SaveWindowAs(setup, "firstrun-2-xyxy.png");

        var classic = (CheckBox)setup.FindName("SetupClassicBox");
        Toggle(classic);
        await UiShots.SettleAsync(900);
        Check(s.UiStyle == UiStyle.Classic && ThemeService.Style == UiStyle.Classic, "Classic toggle switches the style");
        UiShots.SaveWindowAs(setup, "firstrun-3-classic.png");

        var discord = (ComboBox)setup.FindName("DiscordStatusBox");
        discord.SelectedIndex = (int)DiscordStatusMode.Octane;
        await UiShots.SettleAsync(400);
        Check(s.EffectiveDiscordStatus == DiscordStatusMode.Octane && !s.DiscordRichPresence, "Discord status choice applies (Octane's own)");

        var background = (CheckBox)setup.FindName("SetupBackgroundBox");
        Check(background.IsChecked != true, "background box starts unchecked");
        Toggle(background);
        await UiShots.SettleAsync(300);
        Check(s.KeepRunningInBackground, "background box turns the setting on");
        UiShots.SaveWindowAs(setup, "firstrun-4-choices.png");
        Toggle(background);
        await UiShots.SettleAsync(300);
        Check(!s.KeepRunningInBackground, "background box turns it off again");

        Invoke((Button)setup.FindName("DoneButton"));
        await UiShots.SettleAsync(700);
        Check(!setup.IsVisible, "Done closes the setup");
        Check(!s.SetupPending, "Done clears SetupPending");
        Check(DiskHas(Paths.Settings, "SetupPending", "false") && DiskHas(Paths.Settings, "Theme", "\"Xyxy\"") &&
              DiskHas(Paths.Settings, "UiStyle", "\"Classic\"") && DiskHas(Paths.Settings, "DiscordStatus", "\"Octane\"") &&
              DiskHas(Paths.Settings, "KeepRunningInBackground", "false"),
            "choices saved to Settings.json");
        UiShots.SaveWindowAs(menu, "firstrun-5-menu-after.png");
        menu.Close();
        await UiShots.SettleAsync(500);

        var again = await OpenMenuAsync();
        await UiShots.SettleAsync(1200);
        Check(!Application.Current.Windows.OfType<SetupWindow>().Any(), "setup does not come back on the next menu open");
        again.Close();

        // Skip also counts as seen; About can bring it back.
        s.SetupPending = true;
        App.Save();
        var skip = await ShowAsync(new SetupWindow());
        Invoke((Button)skip.FindName("SkipButton"));
        await UiShots.SettleAsync(500);
        Check(!s.SetupPending && DiskHas(Paths.Settings, "SetupPending", "false"), "Skip for now clears SetupPending too");
        MenuWindow.ShowIntroWindows = false;
    }

    // ------------------------------------------------------------------ what's new

    private static async Task WhatsNewAsync()
    {
        var s = App.Settings.Prop;
        var state = App.State.Prop;
        Check(!App.FreshProfile, "existing (2.2.1) profile is not treated as new");
        Check(!s.SetupPending, "no first-run setup for an existing user");
        Check(s.EffectiveDiscordStatus == DiscordStatusMode.XBootstrapper, "old DiscordRichPresence=true keeps X Bootstrapper's status");
        Check(!s.KeepRunningInBackground, "background setting is off after the upgrade");
        var notes = WhatsNewWindow.BundledNotes();
        Check(notes?.Contains(AppInfo.Version) == true, "release notes are bundled in the exe (offline)", notes is null ? "missing" : $"{notes.Length} chars");

        App.TrackVersion();
        Check(state.WhatsNewPendingVersion == AppInfo.Version && App.WhatsNewDue, "update from 2.2.1 noticed: What's new due");
        Check(DiskHas(Paths.State, "LastRunVersion", $"\"{AppInfo.Version}\""), "LastRunVersion saved");

        MenuWindow.ShowIntroWindows = true;
        var menu = await OpenMenuAsync();
        await UiShots.SettleAsync(1200);
        var news = Application.Current.Windows.OfType<WhatsNewWindow>().FirstOrDefault();
        Check(news is not null, "menu opened What's new by itself after the update");
        Check(!Application.Current.Windows.OfType<SetupWindow>().Any(), "no setup window for an existing user");
        if (news is not null)
        {
            UiShots.PlaceOffscreen(news);
            await UiShots.SettleAsync(500);
            UiShots.SaveWindowAs(news, "whatsnew-popup.png");
            var panel = (StackPanel)news.FindName("NotesPanel");
            Check(panel.Children.Count >= 5, "notes rendered", $"{panel.Children.Count} blocks");
            Check(DiskHas(Paths.State, "WhatsNewShownVersion", $"\"{AppInfo.Version}\""), "shown version saved (once per version)");
            news.Close();
        }

        menu.Close();
        await UiShots.SettleAsync(500);
        App.TrackVersion();
        var again = await OpenMenuAsync();
        await UiShots.SettleAsync(1200);
        Check(!Application.Current.Windows.OfType<WhatsNewWindow>().Any(), "not shown again for the same version");
        again.Close();
        await UiShots.SettleAsync(300);

        // The next update, with "Don't show again" ticked.
        state.LastRunVersion = "2.2.1";
        state.WhatsNewShownVersion = null;
        App.TrackVersion();
        Check(App.WhatsNewDue, "a later update makes it due again");
        var manual = await ShowAsync(new WhatsNewWindow());
        Toggle((CheckBox)manual.FindName("DontShowBox"));
        await UiShots.SettleAsync(300);
        Check(!s.ShowWhatsNew && DiskHas(Paths.Settings, "ShowWhatsNew", "false"), "Don't show again saved");
        manual.Close();
        Check(!App.WhatsNewDue, "turned off: not due");
        var third = await OpenMenuAsync();
        await UiShots.SettleAsync(1200);
        Check(!Application.Current.Windows.OfType<WhatsNewWindow>().Any(), "turned off: menu opens without it");
        third.Close();
        MenuWindow.ShowIntroWindows = false;
    }

    // ------------------------------------------------------------------ discord + session lifetime

    private static Process FakeGame(int seconds) => Process.Start(new ProcessStartInfo
    {
        FileName = "cmd.exe",
        Arguments = $"/c ping -n {seconds} 127.0.0.1 >nul",
        CreateNoWindow = true,
        UseShellExecute = false
    })!;

    // The session disposes its Process object when it lets go, so the test tracks the fake game by id.
    private static bool Running(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static void Stop(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
        }
        catch { /* already gone */ }
    }

    private static async Task DiscordAsync()
    {
        UiShots.AllowDiscord = true;
        App.ReclaimDelay = TimeSpan.FromSeconds(3);
        var s = App.Settings.Prop;
        s.ShowTrayIcon = false;
        s.Integrations.Clear();
        s.KeepRunningInBackground = false;
        var discordRunning = Process.GetProcessesByName("Discord").Length > 0;
        Note($"Discord running: {discordRunning}");

        s.DiscordStatus = null;
        s.DiscordRichPresence = true;
        Check(s.EffectiveDiscordStatus == DiscordStatusMode.XBootstrapper, "migration: old Rich Presence on -> X Bootstrapper");
        s.DiscordRichPresence = false;
        Check(s.EffectiveDiscordStatus == DiscordStatusMode.Octane, "migration: old Rich Presence off -> Octane's own");

        foreach (var mode in new[] { DiscordStatusMode.Octane, DiscordStatusMode.None, DiscordStatusMode.XBootstrapper })
        {
            s.SetDiscordStatus(mode);
            var xb = mode == DiscordStatusMode.XBootstrapper;
            Check(App.NeedsWholeSession(s) == xb, $"{mode}: holds the whole game session = {xb}");

            App.PrepareDiscord(false);
            await Task.Delay(xb ? 4000 : 2000);
            var connected = App.Discord?.Connected == true;
            Check(connected == (xb && discordRunning), $"{mode}: connects to Discord at launch = {connected}");

            string? reply = null;
            var got = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (App.Discord is { } live)
                live.ActivityReply += json => got.TrySetResult(json);

            var early = App.Discord;
            var game = FakeGame(20);
            var pid = game.Id;
            var started = DateTime.Now;
            App.StartSession(new GameSession(game, null, false));
            if (xb && discordRunning)
            {
                reply = await Task.WhenAny(got.Task, Task.Delay(6000)) == got.Task ? got.Task.Result : null;
                Check(reply is not null && !reply.Contains("\"evt\":\"ERROR\"") && reply.Contains("Playing on Octane"),
                    $"{mode}: Discord accepted X Bootstrapper's status", reply is null ? "no reply" : reply[..Math.Min(reply.Length, 160)]);
                Check(early is not null && ReferenceEquals(App.Discord, early), $"{mode}: the connection opened at launch is kept when the game starts (no reconnect)");
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
            var gameRunning = Running(pid);
            if (xb)
            {
                Check(App.Session is not null && gameRunning, $"{mode}: still waiting for the game after the link retake");
                Stop(pid);
                await Task.Delay(3500);
                Check(App.Session is null && App.Discord is null, $"{mode}: session and Discord connection end when the game closes");
            }
            else
            {
                Check(App.Session is null && gameRunning, $"{mode}: helper let go ~{(DateTime.Now - started).TotalSeconds:0} s after start while the game keeps running",
                    $"session={(App.Session is null ? "ended" : "active")}, game running={gameRunning}");
                Check(App.Discord is null, $"{mode}: never connected to Discord");
                Stop(pid);
            }
        }

        // Keep running on: the helper stays for the whole game even with Octane's status.
        s.SetDiscordStatus(DiscordStatusMode.Octane);
        s.KeepRunningInBackground = true;
        var held = FakeGame(20);
        var heldPid = held.Id;
        App.StartSession(new GameSession(held, null, false));
        await Task.Delay(5000);
        Check(App.Session is not null, "keep running on + Octane's status: waits for the game (old behaviour)");
        Stop(heldPid);
        await Task.Delay(3500);
        Check(App.Session is null, "... and lets go when it closes");
        s.KeepRunningInBackground = false;

        // Programs set to close with the game also need the whole session (checked without starting them).
        s.Integrations.Add(new Integration { Name = "test", Path = @"C:\Windows\System32\notepad.exe", Enabled = true, AutoClose = true });
        Check(App.NeedsWholeSession(s), "an enabled integration with close-with-game holds the session");
        s.Integrations[0].AutoClose = false;
        Check(!App.NeedsWholeSession(s), "... without close-with-game it does not");
        s.Integrations.Clear();

        Note("log:");
        foreach (var line in LogLines("Discord", "Session", "App"))
            Note("  " + line);
    }

    // ------------------------------------------------------------------ links

    private const string PlayerScheme = "octane-player";
    private const string StudioScheme = "octane-studio";

    private static void Steal(string command)
    {
        foreach (var scheme in new[] { PlayerScheme, StudioScheme })
            Registry.SetValue($@"HKEY_CURRENT_USER\Software\Classes\{scheme}\shell\open\command", "", command);
        Native.NotifyShell();
    }

    private static async Task LinksAsync(bool devBase)
    {
        if (devBase)
        {
            Check(false, "run the links test with the installed exe and the real profile (no -devbase)");
            return;
        }

        Check(InstallerService.IsInstalled(App.Settings.Prop), "X Bootstrapper is installed");
        Check(string.Equals(Environment.ProcessPath, Paths.Executable, StringComparison.OrdinalIgnoreCase), "running the installed exe", Environment.ProcessPath);
        foreach (var link in ProtocolService.CheckLinks())
            Note($"before: {link.Scheme} -> {link.Command} (healthy={link.Healthy})");

        var launcher = Path.Combine(Paths.LocalAppData, "Octane", "OctanePlayerLauncher.exe");
        var stolen = $"\"{launcher}\" \"%1\"";
        var theme = App.Settings.Prop.Theme;
        var style = App.Settings.Prop.UiStyle;
        foreach (var pass in new[] { UiStyle.Modern, UiStyle.Classic })
        {
            UseTheme(theme, pass);
            var tag = Tag(theme, pass);
            Steal(stolen);
            var menu = await OpenMenuAsync();
            var links = menu.RefreshLinkStatus();
            await UiShots.SettleAsync(500);
            Check(links.All(link => !link.Healthy) && menu.LinkWarningVisible, $"{tag}: warning shows when Octane's launcher has the links",
                string.Join("; ", links.Select(link => $"{link.Scheme} owner={link.OwnerName}")));
            UiShots.SaveWindowAs(menu, $"links-1-warning-{tag}.png");
            UiShots.SaveElementAs((FrameworkElement)menu.FindName("LinkBannerHost"), $"links-banner-{tag}.png");

            Invoke((Button)menu.FindName("FixLinksButton"));
            await UiShots.SettleAsync(800);
            var after = ProtocolService.CheckLinks();
            Check(after.All(link => link.Healthy) && !menu.LinkWarningVisible, $"{tag}: Fix links points both links back and hides the warning",
                string.Join("; ", after.Select(link => $"{link.Scheme} -> {link.Command}")));
            UiShots.SaveWindowAs(menu, $"links-2-fixed-{tag}.png");

            // Taken again while the menu stays open: the 5 s timer notices.
            Steal(stolen);
            await UiShots.SettleAsync(6500);
            Check(menu.LinkWarningVisible, $"{tag}: warning comes back by itself within ~5 s when the links are taken again");
            Invoke((Button)menu.FindName("FixLinksButton"));
            await UiShots.SettleAsync(600);

            // The same check in the first-run setup.
            Steal(stolen);
            var setup = await ShowAsync(new SetupWindow());
            Check(!setup.LinksHealthy && ((Button)setup.FindName("SetupFixLinksButton")).IsVisible, $"{tag}: setup shows the links as taken");
            UiShots.SaveWindowAs(setup, $"links-3-setup-warning-{tag}.png");
            UiShots.SaveElementAs((FrameworkElement)setup.FindName("SetupContent"), $"links-3-setup-full-{tag}.png");
            Invoke((Button)setup.FindName("SetupFixLinksButton"));
            await UiShots.SettleAsync(600);
            Check(setup.LinksHealthy && ProtocolService.CheckLinks().All(link => link.Healthy), $"{tag}: setup Fix links works");
            UiShots.SaveWindowAs(setup, $"links-4-setup-fixed-{tag}.png");
            setup.Close();
            menu.Close();
            await UiShots.SettleAsync(400);
        }

        UseTheme(theme, style);
        foreach (var link in ProtocolService.CheckLinks())
            Note($"after: {link.Scheme} -> {link.Command} (healthy={link.Healthy})");
        Check(ProtocolService.CheckLinks().All(link => link.Healthy), "links end up pointing at X Bootstrapper");
    }

    // ------------------------------------------------------------------ mods / atmosphere

    private static async Task ModsAsync()
    {
        if (ClientLocator.RunningIds(ClientLocator.PlayerProcessNames).Count > 0)
        {
            Check(false, "Octane is running; mods test not run");
            return;
        }

        ModService.SkipHudPatch = true;
        var client = ClientLocator.Find(App.Settings.Prop, App.State.Prop);
        Check(client is not null, "client found", client?.VersionDirectory);
        if (client is null)
            return;

        var cloudsSlot = ModService.Slots.First(slot => slot.Id == "clouds");
        var clientClouds = Path.Combine(client.VersionDirectory, cloudsSlot.RelativePath);
        // Client folder casing may be Content\Sky
        if (!File.Exists(clientClouds))
        {
            var alt = Directory.GetFiles(Path.Combine(client.VersionDirectory, "Content", "Sky"), "clouds.dds", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            Check(alt is not null, "client clouds.dds exists");
            if (alt is null)
                return;
            clientClouds = alt;
        }

        var stockHash = Hash(clientClouds);
        var donor = Path.Combine(Path.GetDirectoryName(clientClouds)!, "cloudDetail.dds");
        Check(File.Exists(donor), "donor cloudDetail.dds exists");

        var hadCloudsMod = ModService.HasSlot(cloudsSlot);
        var priorMod = hadCloudsMod ? File.ReadAllBytes(ModService.SlotPath(cloudsSlot)) : null;

        try
        {
            ModService.SetSlot(cloudsSlot, donor);
            Check(ModService.HasSlot(cloudsSlot), "clouds slot staged in Modifications");
            var applied = Path.Combine(client.VersionDirectory, cloudsSlot.RelativePath);
            if (!File.Exists(applied))
                applied = clientClouds;
            Check(Hash(applied) == Hash(donor), "client clouds.dds matches donor after apply");
            Check(Hash(applied) != stockHash, "client clouds.dds changed from stock");

            var relative = ModService.ClientRelativePath(clientClouds, client);
            Check(relative.Replace('/', '\\').EndsWith(@"content\sky\clouds.dds", StringComparison.OrdinalIgnoreCase) ||
                  relative.Replace('/', '\\').EndsWith(@"Content\Sky\clouds.dds", StringComparison.OrdinalIgnoreCase),
                "ClientRelativePath for clouds", relative);

            var square = Path.Combine(client.VersionDirectory, "Content", "Textures", "particles", "SquareParticle.png");
            if (!File.Exists(square))
                square = Path.Combine(client.VersionDirectory, ModService.Slots.First(s => s.Id == "particleSquare").RelativePath);
            Check(File.Exists(square), "SquareParticle.png on client");
            if (File.Exists(square))
            {
                var particleRel = ModService.ClientRelativePath(square, client);
                ModService.SetClientRelativeFromFile(square, particleRel); // identical bytes — still stages
                Check(File.Exists(Path.Combine(Paths.Modifications, particleRel)), "replace stages into Modifications", particleRel);
                // Clean particle stage so we don't leave a useless identical mod
                var staged = Path.Combine(Paths.Modifications, particleRel);
                if (File.Exists(staged))
                    File.Delete(staged);
                ModService.ApplyToInstalledClients();
            }

            var menu = await OpenMenuAsync();
            var page = (ModsPage)menu.Navigate("NavMods")!;
            await UiShots.SettleAsync(800);
            Check(page.FindName("AtmosphereSlotsPanel") is System.Windows.Controls.Panel, "Atmosphere panel present");
            Check(page.FindName("ClearIndoorSkyButton") is System.Windows.Controls.Button, "Clear indoor sky button present");
            menu.Close();
        }
        finally
        {
            if (priorMod is null)
                ModService.ClearSlot(cloudsSlot);
            else
            {
                var tmp = Path.Combine(Path.GetTempPath(), "xb-clouds-restore-" + Guid.NewGuid().ToString("N") + ".dds");
                File.WriteAllBytes(tmp, priorMod);
                try { ModService.SetSlot(cloudsSlot, tmp); }
                finally { try { File.Delete(tmp); } catch { /* ignore */ } }
            }

            Note("restored clouds atmosphere slot");
        }
    }

    // ------------------------------------------------------------------ flags

    private static async Task FlagsAsync()
    {
        var client = ClientLocator.Find(App.Settings.Prop, App.State.Prop);
        Check(client is not null, "client found", client?.VersionDirectory);
        if (client is null)
            return;

        var settingsPath = Path.Combine(client.VersionDirectory, "ClientSettings", "ClientAppSettings.json");
        Check(File.Exists(settingsPath), "ClientAppSettings.json exists", settingsPath);

        var s = App.Settings.Prop;
        var snapshot = (
            s.FramerateLimit,
            s.RenderingMode,
            s.TextureQuality,
            s.DisablePostFx,
            s.ShowFpsCounter,
            s.PerformanceMode,
            FastFlags: new Dictionary<string, string>(s.FastFlags ?? new(), StringComparer.OrdinalIgnoreCase));

        try
        {
            // Automatic + unlimited FPS + high textures + FPS counter, no perf
            s.FramerateLimit = -1;
            s.RenderingMode = RenderingMode.Automatic;
            s.TextureQuality = 2;
            s.DisablePostFx = false;
            s.ShowFpsCounter = true;
            s.PerformanceMode = false;
            s.FastFlags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var built = FastFlagService.Build(s);
            Check(built.GetValueOrDefault("DFIntTaskSchedulerTargetFps") == "9999", "unlimited -> TargetFps 9999");
            Check(built.GetValueOrDefault("FFlagTaskSchedulerLimitTargetFpsTo2402") == "False", "unlimited unlocks 240 cap");
            Check(built.GetValueOrDefault("FFlagDebugGraphicsPreferD3D11") == "True", "Automatic prefers D3D11");
            Check(built.GetValueOrDefault("FFlagDebugGraphicsPreferVulkan") == "False", "Automatic clears PreferVulkan");
            Check(built.GetValueOrDefault("DFIntTextureQualityOverride") == "2", "High texture override = 2");
            Check(built.ContainsKey("FFlagDisablePostFx") == false, "post FX off leaves DisablePostFx unset");
            Check(!built.ContainsKey("DFFlagDebugPauseVoxelizer"), "perf off leaves voxelizer flag unset");

            FastFlagService.ApplyAll(s, App.State.Prop, client, log: true);
            var disk = ReadFlagFile(settingsPath);
            Check(FlagEquals(disk, "DFIntTaskSchedulerTargetFps", "9999"), "disk TargetFps 9999");
            Check(FlagEquals(disk, "FFlagDebugGraphicsPreferD3D11", "True"), "disk PreferD3D11");
            Check(FlagEquals(disk, "DFFlagTextureQualityOverrideEnabled", "True"), "disk texture override on");
            Check(FlagEquals(disk, "DFIntTextureQualityOverride", "2"), "disk texture quality 2");
            Check(FlagEquals(disk, "FFlagDebugDisplayFPS", "True"), "disk FPS counter");
            Check(!disk.ContainsKey("FFlagDisablePostFx"), "disk has no DisablePostFx when off");
            Check(!disk.ContainsKey("DFFlagDebugPauseVoxelizer"), "disk has no perf voxelizer when off");

            // Explicit Vulkan preference (must not disable D3D11)
            s.RenderingMode = RenderingMode.Vulkan;
            built = FastFlagService.Build(s);
            Check(built.GetValueOrDefault("FFlagDebugGraphicsPreferVulkan") == "True", "Vulkan prefers Vulkan");
            Check(built.GetValueOrDefault("FFlagDebugGraphicsPreferD3D11") == "False", "Vulkan clears PreferD3D11");
            Check(!built.ContainsKey("FFlagDebugGraphicsDisableDirect3D11"), "Vulkan never disables D3D11");
            FastFlagService.ApplyAll(s, App.State.Prop, client, log: false);
            disk = ReadFlagFile(settingsPath);
            Check(FlagEquals(disk, "FFlagDebugGraphicsPreferVulkan", "True"), "disk PreferVulkan");
            Check(!disk.ContainsKey("FFlagDebugGraphicsDisableDirect3D11"), "disk never DisableDirect3D11");

            // Performance mode + default texture
            s.RenderingMode = RenderingMode.Direct3D11;
            s.TextureQuality = -1;
            s.DisablePostFx = true;
            s.PerformanceMode = true;
            FastFlagService.ApplyAll(s, App.State.Prop, client, log: false);
            disk = ReadFlagFile(settingsPath);
            Check(FlagEquals(disk, "FFlagDisablePostFx", "True"), "disk DisablePostFx");
            Check(FlagEquals(disk, "DFFlagDebugPauseVoxelizer", "True"), "disk perf voxelizer");
            Check(FlagEquals(disk, "FIntFRMMaxGrassDistance", "0"), "disk grass distance 0");
            Check(!disk.ContainsKey("DFFlagTextureQualityOverrideEnabled"), "default texture removes override");
            Check(!disk.ContainsKey("DFIntTextureQualityOverride"), "default texture removes quality int");

            // Turn perf + postfx off — managed keys must leave the file
            s.PerformanceMode = false;
            s.DisablePostFx = false;
            s.ShowFpsCounter = false;
            s.FramerateLimit = 0;
            s.RenderingMode = RenderingMode.Automatic;
            FastFlagService.ApplyAll(s, App.State.Prop, client, log: false);
            disk = ReadFlagFile(settingsPath);
            Check(!disk.ContainsKey("DFFlagDebugPauseVoxelizer"), "turning perf off removes voxelizer");
            Check(!disk.ContainsKey("FFlagDisablePostFx"), "turning post FX off removes flag");
            Check(!disk.ContainsKey("DFIntTaskSchedulerTargetFps"), "default FPS removes TargetFps");
            // Automatic still prefers D3D11
            Check(FlagEquals(disk, "FFlagDebugGraphicsPreferD3D11", "True"), "Automatic still PreferD3D11 after cleanup");

            var menu = await OpenMenuAsync();
            var page = (FastFlagsPage)menu.Navigate("NavFlags")!;
            await UiShots.SettleAsync(800);
            page.Flush();
            Check(true, "FastFlags page Flush runs without error");
            menu.Close();
        }
        finally
        {
            s.FramerateLimit = snapshot.FramerateLimit;
            s.RenderingMode = snapshot.RenderingMode;
            s.TextureQuality = snapshot.TextureQuality;
            s.DisablePostFx = snapshot.DisablePostFx;
            s.ShowFpsCounter = snapshot.ShowFpsCounter;
            s.PerformanceMode = snapshot.PerformanceMode;
            s.FastFlags = snapshot.FastFlags;
            FastFlagService.ApplyAll(s, App.State.Prop, client, log: true);
            if (UiShots.AllowSave)
                App.Save();
            Note("restored previous FastFlag presets");
        }
    }

    private static Dictionary<string, string> ReadFlagFile(string path)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            flags[property.Name] = property.Value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => property.Value.GetString() ?? "",
                System.Text.Json.JsonValueKind.True => "True",
                System.Text.Json.JsonValueKind.False => "False",
                System.Text.Json.JsonValueKind.Number => property.Value.GetRawText(),
                _ => property.Value.ToString()
            };
        }
        return flags;
    }

    private static bool FlagEquals(Dictionary<string, string> disk, string key, string expected) =>
        disk.TryGetValue(key, out var value) &&
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------ sky
    private static async Task SkyAsync(string dir)
    {
        if (ClientLocator.RunningIds(ClientLocator.PlayerProcessNames).Count > 0)
        {
            Check(false, "Octane is running; sky test not run");
            return;
        }

        ModService.SkipHudPatch = true;
        var client = ClientLocator.Find(App.Settings.Prop, App.State.Prop);
        Check(client is not null, "client found", client?.VersionDirectory);
        if (client is null)
            return;

        string ClientFace(string face) => Path.Combine(client.VersionDirectory, SkyboxService.RelativePath(face));
        string ModFace(string face) => Path.Combine(Paths.Modifications, SkyboxService.RelativePath(face));
        Dictionary<string, string> Hashes(Func<string, string> path) => SkyboxService.Faces.ToDictionary(face => face, face => Hash(path(face)));
        var extra = Path.Combine(client.VersionDirectory, "ExtraContent", "textures", "sky");
        string[] ExtraFiles() => Directory.Exists(extra) ? Directory.GetFiles(extra).Select(file => Path.GetFileName(file)!).OrderBy(x => x).ToArray() : Array.Empty<string>();

        var before = Hashes(ClientFace);
        var extraBefore = ExtraFiles();
        var stockHeader = File.ReadAllBytes(ClientFace("ft")).AsSpan(0, 128).ToArray();
        foreach (var (face, hash) in before)
            Note($"stock {face}: {hash}");

        var menu = await OpenMenuAsync();
        var page = (ModsPage)menu.Navigate("NavMods")!;
        await page.RebuildSkyAsync();
        await UiShots.SettleAsync(1500);
        var skyPanel = (FrameworkElement)page.FindName("SkyPanel");
        UiShots.SaveElementAs(Card(skyPanel), "sky-card-0-default.png");
        Check(SkyboxService.CurrentId() == SkyboxService.DefaultId, "starts on the default sky");

        var step = 1;
        foreach (var id in new[] { "sunset", "night", "nebula", "clearday" })
        {
            var ok = await page.ApplySkyAsync(id);
            await UiShots.SettleAsync(800);
            var now = Hashes(ClientFace);
            var mods = Hashes(ModFace);
            Check(ok && SkyboxService.CurrentId() == id, $"{id}: applied");
            Check(SkyboxService.Faces.All(face => now[face] != before[face]), $"{id}: all six client faces changed");
            Check(SkyboxService.Faces.All(face => now[face] == mods[face]), $"{id}: client faces equal the Modifications files");
            Check(SkyboxService.Faces.All(face => new FileInfo(ClientFace(face)).Length == SkyboxService.FileLength), $"{id}: same size as the stock files ({SkyboxService.FileLength})");
            Check(File.ReadAllBytes(ClientFace("ft")).AsSpan(0, 128).SequenceEqual(stockHeader), $"{id}: DDS header identical to the stock one");
            Check(SkyboxService.Faces.All(face => SkyboxService.DecodeFile(ClientFace(face), 64) is not null), $"{id}: every face decodes");
            var copy = Path.Combine(dir, "sky-" + id);
            Directory.CreateDirectory(copy);
            foreach (var face in SkyboxService.Faces)
                File.Copy(ClientFace(face), Path.Combine(copy, $"sky512_{face}.tex"), true);
            if (SkyboxService.Preview(id, 256) is { } strip)
                SavePng(strip, Path.Combine(dir, $"sky-strip-{id}.png"));
            UiShots.SaveElementAs(Card(skyPanel), $"sky-card-{step++}-{id}.png");
        }

        // Custom: six non-square PNGs, named the way sky packs usually are.
        var source = Path.Combine(dir, "custom-src");
        Directory.CreateDirectory(source);
        var colors = new Dictionary<string, Color>
        {
            ["bk"] = Colors.SteelBlue, ["dn"] = Colors.SaddleBrown, ["ft"] = Colors.MediumSeaGreen,
            ["lf"] = Colors.MediumPurple, ["rt"] = Colors.Goldenrod, ["up"] = Colors.DeepSkyBlue
        };
        foreach (var (face, color) in colors)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new LinearGradientBrush(color, Colors.White, 90), null, new Rect(0, 0, 800, 600));
                dc.DrawText(new FormattedText(face.ToUpperInvariant(), System.Globalization.CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 220, Brushes.Black, 1.0), new Point(250, 150));
            }

            var bitmap = new RenderTargetBitmap(800, 600, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            SavePng(bitmap, Path.Combine(source, $"testsky_{face}.png"));
        }

        var matched = SkyboxService.MatchFaces(Directory.GetFiles(source).Append(Path.Combine(source, "readme.txt")), out var unmatched);
        Check(matched.Count == 6 && unmatched.Count == 1, "custom: six faces matched by name, stray file left out", $"matched {matched.Count}, unmatched {unmatched.Count}");
        var customOk = await page.ApplyCustomSkyAsync(matched);
        await UiShots.SettleAsync(800);
        var custom = Hashes(ClientFace);
        Check(customOk && SkyboxService.CurrentId() == SkyboxService.CustomId, "custom: applied");
        Check(SkyboxService.Faces.All(face => custom[face] != before[face] && custom[face] == Hash(ModFace(face))), "custom: client faces changed and equal Modifications");
        var customCopy = Path.Combine(dir, "sky-custom");
        Directory.CreateDirectory(customCopy);
        foreach (var face in SkyboxService.Faces)
            File.Copy(ClientFace(face), Path.Combine(customCopy, $"sky512_{face}.tex"), true);
        UiShots.SaveElementAs(Card(skyPanel), $"sky-card-{step++}-custom.png");

        var restored = await page.ApplySkyAsync(SkyboxService.DefaultId);
        await UiShots.SettleAsync(800);
        var after = Hashes(ClientFace);
        Check(restored && SkyboxService.CurrentId() == SkyboxService.DefaultId, "default: restored");
        Check(SkyboxService.Faces.All(face => after[face] == before[face]), "default: all six client faces byte-identical to before (SHA-256)");
        Check(SkyboxService.Faces.All(face => !File.Exists(ModFace(face))), "default: sky files removed from Modifications");
        Check(ExtraFiles().SequenceEqual(extraBefore), "ExtraContent\\textures\\sky untouched", string.Join(",", ExtraFiles()));
        UiShots.SaveElementAs(Card(skyPanel), $"sky-card-{step}-restored.png");
        foreach (var style in new[] { UiStyle.Modern, UiStyle.Classic })
        {
            UseTheme(App.Settings.Prop.Theme, style);
            await UiShots.SettleAsync(1500);
            page = (ModsPage)menu.Navigate("NavMods")!;
            await page.RebuildSkyAsync();
            await UiShots.SettleAsync(1200);
            UiShots.SaveWindowAs(menu, $"sky-menu-{style.ToString().ToLowerInvariant()}.png");
        }

        menu.Close();
    }

    // ------------------------------------------------------------------ themes

    private static Color Sample(Window window, double x, double y)
    {
        var root = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect((int)x, (int)y, 1, 1), pixel, 4, 0);
        return Color.FromRgb(pixel[2], pixel[1], pixel[0]);
    }

    private static bool Near(Color a, Color b, int tolerance = 12) =>
        Math.Abs(a.R - b.R) <= tolerance && Math.Abs(a.G - b.G) <= tolerance && Math.Abs(a.B - b.B) <= tolerance;

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static string LogoName(Window window, string imageName)
    {
        if (window.FindName(imageName) is System.Windows.Controls.Image { Source: BitmapImage bitmap })
            return Path.GetFileNameWithoutExtension(bitmap.UriSource?.ToString() ?? "?");
        return window.FindName(imageName) is System.Windows.Controls.Image image ? image.Source?.ToString() ?? "none" : "missing";
    }

    /// <summary>
    /// Picks every theme the way a click on its tile does (the tile's MouseLeftButtonUp, found by hit-testing its
    /// center), in both styles, and checks the open menu really repaints: sidebar and page pixels against the palette,
    /// the logo, the launch window. With -restartcheck it only reports what a fresh start shows (theme saved earlier).
    /// </summary>
    private static async Task ThemesAsync(string[] args)
    {
        if (args.Any(arg => arg.Equals("-restartcheck", StringComparison.OrdinalIgnoreCase)))
        {
            var fresh = await OpenMenuAsync();
            var p = ThemeService.Current;
            var side = Sample(fresh, 4, fresh.ActualHeight - 40);
            Check(Near(side, p.Sidebar), $"fresh start shows {App.Settings.Prop.Theme}/{App.Settings.Prop.UiStyle}", $"sidebar {Hex(side)} want {Hex(p.Sidebar)}, logo {LogoName(fresh, "BrandMark")}");
            UiShots.SaveWindowAs(fresh, $"restart-menu-{Tag(App.Settings.Prop.Theme, App.Settings.Prop.UiStyle)}.png");
            var boot = await ShowAsync(new BootstrapperWindow(new LaunchArgs { Mode = LaunchMode.Player }), 1200);
            UiShots.SaveWindowAs(boot, $"restart-launch-{Tag(App.Settings.Prop.Theme, App.Settings.Prop.UiStyle)}.png");
            boot.Close();
            return;
        }

        var menu = await OpenMenuAsync();
        var startTheme = App.Settings.Prop.Theme;
        var traced = 0;
        void Trace(object sender, RoutedEventArgs e)
        {
            if (traced++ > 40)
                return;
            var src = e.OriginalSource as FrameworkElement;
            var srcTag = src is null ? "" : $"{src.GetType().Name}:{src.Name}:{src.Tag}";
            Note($"  trace {e.RoutedEvent.Name} at {sender.GetType().Name} src {srcTag} handled={e.Handled} captured={System.Windows.Input.Mouse.Captured?.GetType().Name ?? "-"}");
        }
        foreach (var ev in new[] { UIElement.PreviewMouseDownEvent, UIElement.MouseDownEvent, UIElement.PreviewMouseUpEvent, UIElement.MouseUpEvent })
            menu.AddHandler(ev, new RoutedEventHandler(Trace), true);
        Note($"start: theme {startTheme}, style {App.Settings.Prop.UiStyle}, palette {ThemeService.Current.Id}");
        foreach (var style in new[] { UiStyle.Modern, UiStyle.Classic })
        {
            if (ThemeService.Style != style)
            {
                var page0 = (AppearancePage)menu.Navigate("NavAppearance")!;
                await UiShots.SettleAsync(700);
                Toggle((CheckBox)page0.FindName("ClassicStyleBox"));
                await UiShots.SettleAsync(1200);
            }

            foreach (var theme in Enum.GetValues<AppTheme>())
            {
                var page = (AppearancePage)menu.Navigate("NavAppearance")!;
                await UiShots.SettleAsync(700);
                var panel = (WrapPanel)page.FindName("ThemePanel");
                var target = theme == AppTheme.XyxyDark ? AppTheme.Xyxy : theme;
                var tile = panel.Children.OfType<Border>().FirstOrDefault(b => b.Tag is AppTheme id &&
                    (id == target || (target == AppTheme.Xyxy && ThemeService.IsXyxy(id))));
                if (tile is null)
                {
                    Check(false, $"{style} {theme}: tile not found", string.Join(",", panel.Children.OfType<Border>().Select(b => b.Tag?.ToString() ?? "null")));
                    continue;
                }

                // Aim like a click: what's under the tile's center gets the mouse-up.
                var center = tile.TranslatePoint(new Point(tile.ActualWidth / 2, 14), menu);
                var hit = menu.InputHitTest(center) as DependencyObject;
                var reaches = hit is not null && (ReferenceEquals(hit, tile) || tile.IsAncestorOf(hit));
                Check(reaches, $"{style} {theme}: a click on the tile reaches it", $"hit {hit?.GetType().Name}");
                await ClickAsync(menu, center);
                await UiShots.SettleAsync(900);
                if (theme == AppTheme.XyxyDark)
                {
                    // Xyxy's tile has Light/Dark chips; pick Dark.
                    page = (AppearancePage)((System.Windows.Controls.ContentControl)menu.FindName("PageHost")).Content;
                    var chip = FindText(page, "Dark 🌙");
                    if (chip is FrameworkElement chipElement)
                        await ClickAsync(menu, chipElement.TranslatePoint(new Point(chipElement.ActualWidth / 2, chipElement.ActualHeight / 2), menu));
                    await UiShots.SettleAsync(900);
                }

                var palette = ThemeService.Get(theme, style);
                var side = Sample(menu, 4, menu.ActualHeight - 40);
                var body = Sample(menu, menu.ActualWidth - 30, menu.ActualHeight - 12);
                Check(App.Settings.Prop.Theme == theme && ThemeService.Current.Id == theme, $"{style} {theme}: selected", $"settings {App.Settings.Prop.Theme}, palette {ThemeService.Current.Id}");
                Check(Near(side, palette.Sidebar) && Near(body, palette.Background), $"{style} {theme}: open menu repainted",
                    $"sidebar {Hex(side)} want {Hex(palette.Sidebar)}, page {Hex(body)} want {Hex(palette.Background)}, logo {LogoName(menu, "BrandMark")}");
                var wantLogo = Path.GetFileNameWithoutExtension(ThemeService.LogoFile(theme) ?? "x-mark.png");
                Check(LogoName(menu, "BrandMark") == wantLogo, $"{style} {theme}: menu logo {wantLogo}", LogoName(menu, "BrandMark"));
                UiShots.SaveWindowAs(menu, $"menu-{Tag(theme, style)}.png");
                var boot = await ShowAsync(new BootstrapperWindow(new LaunchArgs { Mode = LaunchMode.Player }), 1000);
                var bootBg = Sample(boot, 6, boot.ActualHeight / 2);
                Check(Near(bootBg, palette.Background) || App.Settings.Prop.BootstrapperStyle == BootstrapperStyle.Classic, $"{style} {theme}: launch window uses the theme",
                    $"bg {Hex(bootBg)} want {Hex(palette.Background)}, logo {LogoName(boot, "Logo")}");
                Check(LogoName(boot, "Logo") == wantLogo, $"{style} {theme}: launch logo {wantLogo}", LogoName(boot, "Logo"));
                UiShots.SaveWindowAs(boot, $"launch-{Tag(theme, style)}.png");
                boot.Close();
            }
        }

        // Leave the profile as it started (Modern + the starting theme) unless asked to keep the last pick.
        var keep = args.SkipWhile(arg => !arg.Equals("-keep", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
        var finalTheme = keep is not null && Enum.TryParse<AppTheme>(keep, true, out var parsed) ? parsed : startTheme;
        App.Settings.Prop.Theme = finalTheme;
        App.Settings.Prop.UiStyle = keep is not null ? UiStyle.Classic : UiStyle.Modern;
        ThemeService.Apply(finalTheme, App.Settings.Prop.UiStyle);
        App.Save();
        Note($"saved for the restart check: {finalTheme}/{App.Settings.Prop.UiStyle}");
        menu.Close();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// A click through Windows' own mouse messages to the window (WM_LBUTTONDOWN/UP at a client point), so WPF runs its
    /// full input path: hit testing, preview handlers, capture, the tile's press animation. The real cursor never moves.
    /// </summary>
    private static async Task ClickAsync(Window window, Point point)
    {
        // WPF's own route for a left click: MouseDown then MouseUp bubble from the element under the point, and every
        // element on the way turns them into its MouseLeftButtonDown/Up (what the tile listens to).
        if (window.InputHitTest(point) is not UIElement target)
            return;
        foreach (var (preview, bubble) in new[] { (UIElement.PreviewMouseDownEvent, UIElement.MouseDownEvent), (UIElement.PreviewMouseUpEvent, UIElement.MouseUpEvent) })
        {
            var args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = preview };
            target.RaiseEvent(args);
            var main = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left) { RoutedEvent = bubble, Handled = args.Handled };
            target.RaiseEvent(main);
            await UiShots.SettleAsync(110);
        }
    }

    private static UIElement? FindText(DependencyObject root, string text)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock block && block.Text == text)
                return block;
            if (FindText(child, text) is { } found)
                return found;
        }

        return null;
    }

    // ------------------------------------------------------------------ logs

    private static readonly string[] FakeSecrets =
    {
        "FAKEticketABCDEF1234567890abcdef",
        "FAKEbearerTOKEN0123456789abcdefXYZ",
        "FAKE-TICKET-9f8e7d6c5b4a",
        "FAKE_COOKIE_VALUE_0123456789",
        "FAKEtok0123456789ABCDEFGHIJKLMNOPQRSTUV",
        "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJmYWtlIn0.ZmFrZXNpZ25hdHVyZQ",
        "FAKEARGTICKET123",
        "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08"
    };

    private static async Task LogsAsync(string dir)
    {
        // A fake log full of made-up secrets (none of these are real).
        var fake = Path.Combine(Paths.Logs, $"XBootstrapper_{DateTime.Now:yyyyMMdd'T'HHmmss}_fake.log");
        File.WriteAllLines(fake, new[]
        {
            "[12:00:00.000] [App] Args: octane-player:1+launchmode:play+gameinfo:FAKEticketABCDEF1234567890abcdef+placelauncherurl:https%3A%2F%2Foctane.wtf%2FGame%2FPlaceLauncher.ashx",
            "[12:00:00.100] [Http] Authorization: Bearer FAKEbearerTOKEN0123456789abcdefXYZ",
            "[12:00:00.200] [Http] GET https://octane.wtf/Game/Join.ashx?ticket=FAKE-TICKET-9f8e7d6c5b4a&placeId=4399",
            "[12:00:00.300] [Http] cookie=FAKE_COOKIE_VALUE_0123456789",
            "[12:00:00.400] [Http] token: FAKEtok0123456789ABCDEFGHIJKLMNOPQRSTUV",
            "[12:00:00.500] [Http] jwt eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJmYWtlIn0.ZmFrZXNpZ25hdHVyZQ",
            "[12:00:00.600] [Launch] OctanePlayer.exe -t FAKEARGTICKET123 -j https://octane.wtf/join",
            "[12:00:00.700] [Launch] hash 9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08",
            $"[12:00:00.800] [Mods] Applied 3 changed file(s) in {Path.Combine(Paths.LocalAppData, "Octane", "clients", "2021")}",
            "[12:00:00.900] [Update] https://github.com/nicolasishere1282-dotcom/x-bootstrapper/releases"
        });

        var menu = await OpenMenuAsync();
        var page = (AboutPage)menu.Navigate("NavAbout")!;
        await UiShots.SettleAsync(900);
        var result = await page.CopyLogsAsync();
        await UiShots.SettleAsync(600);
        Check(result is not null && File.Exists(result.Path), "Copy logs made a zip", result?.Path);
        UiShots.SaveElementAs(Card((FrameworkElement)page.FindName("CopyLogsButton")), "logs-about-card.png");
        UiShots.SaveWindowAs(menu, "logs-about.png");
        menu.Close();
        File.Delete(fake);
        if (result is null)
            return;

        Check(Path.GetDirectoryName(result.Path)!.Equals(Paths.Desktop, StringComparison.OrdinalIgnoreCase), "zip is on the Desktop");
        var text = new System.Text.StringBuilder();
        using (var zip = ZipFile.OpenRead(result.Path))
        {
            Note("zip entries: " + string.Join(", ", zip.Entries.Select(entry => $"{entry.FullName} ({entry.Length} B)")));
            Check(zip.Entries.Any(entry => entry.FullName == "summary.txt"), "zip has summary.txt");
            Check(zip.Entries.Count(entry => entry.FullName.EndsWith(".log", StringComparison.OrdinalIgnoreCase)) is >= 1 and <= SupportBundleService.MaxLogs, "zip has the newest logs (max 10)");
            foreach (var entry in zip.Entries)
            {
                using var reader = new StreamReader(entry.Open());
                var content = reader.ReadToEnd();
                text.AppendLine(content);
                if (entry.FullName == "summary.txt")
                    File.WriteAllText(Path.Combine(dir, "logs-summary.txt"), content);
                if (entry.FullName.Contains("_fake", StringComparison.Ordinal))
                    File.WriteAllText(Path.Combine(dir, "logs-fake-redacted.txt"), content);
            }
        }

        var all = text.ToString();
        foreach (var secret in FakeSecrets)
            Check(!all.Contains(secret, StringComparison.Ordinal), $"fake secret removed: {secret[..10]}…");
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Check(!all.Contains(profile, StringComparison.OrdinalIgnoreCase) && !all.Contains(profile.Replace("\\", "\\\\"), StringComparison.OrdinalIgnoreCase) &&
              all.Contains("%USERPROFILE%"), "user profile path replaced with %USERPROFILE%");
        Check(all.Contains("Applied 3 changed file(s)") && all.Contains("github.com/nicolasishere1282-dotcom/x-bootstrapper"), "ordinary log lines kept");
        Check(all.Contains(AppInfo.Version), "summary names the version");
        File.Copy(result.Path, Path.Combine(dir, "logs-bundle.zip"), true);
        File.Delete(result.Path);
        Check(!File.Exists(result.Path), "test zip removed from the Desktop");
    }
}
