using System.Windows;
using Caelus.Core;
using Caelus.Models;
using Caelus.Services;
using Caelus.UI;

namespace Caelus;

public partial class App : System.Windows.Application
{
    public static JsonStore<Settings> Settings { get; private set; } = null!;
    public static JsonStore<AppState> State { get; private set; } = null!;
    public static LaunchArgs Args { get; private set; } = new();
    public static DiscordService? Discord { get; set; }
    public static GameSession? Session { get; private set; }
    public static bool SuppressSave { get; set; }

    private Mutex? _mutex;
    private static bool _startupComplete;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            Logger.Error("App", args.Exception);
            try
            {
                System.Windows.MessageBox.Show(args.Exception.Message, AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                /* nothing left to show it on */
            }

            // ShutdownMode is OnExplicitShutdown: never leave an invisible process behind.
            if (!_startupComplete)
                Shutdown(1);
            else
                RequestExitIfIdle();
        };

        try
        {
            StartupCore(e);
        }
        catch (Exception ex)
        {
            Logger.Error("App", ex);
            try
            {
                System.Windows.MessageBox.Show(ex.Message, AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                /* ignore */
            }

            Shutdown(1);
        }
        finally
        {
            _startupComplete = true;
        }
    }

    /// <summary>
    /// Exits once no window is visible and nothing is running in the background. Every window calls
    /// this when it closes, so closing the menu with Alt+F4 or from the taskbar can't strand a process.
    /// </summary>
    public static void RequestExitIfIdle()
    {
        var app = Current;
        if (app is null)
            return;

        if (UiShots.Active && !UiShots.AllowExit)
            return;

        app.Dispatcher.BeginInvoke(() =>
        {
            if (app.Windows.OfType<Window>().Any(window => window.IsVisible))
                return;
            if (Session is not null)
                return;
            if (ShouldStayInBackground((App)app))
            {
                EnterBackground();
                return;
            }

            Logger.Write("App", "No window open and nothing running: exiting.");
            app.Shutdown();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>True while the menu is closed but "Keep running in the background" holds the process in the tray.</summary>
    public static bool InBackground { get; private set; }

    private static System.Windows.Threading.DispatcherTimer? _backgroundTimer;

    /// <summary>Only the process that owns the menu stays in the background, and only with the setting on.</summary>
    private static bool ShouldStayInBackground(App app) =>
        Settings?.Prop.KeepRunningInBackground == true && app._mutex is not null &&
        (!UiShots.Active || UiShots.AllowBackground);

    private static void EnterBackground()
    {
        if (!InBackground)
            Logger.Write("App", "Menu closed; staying in the background (Keep running in the background is on).");
        InBackground = true;
        TrayService.Show("running in the background");
        if (_backgroundTimer is null)
        {
            _backgroundTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _backgroundTimer.Tick += (_, _) => WatchLinksInBackground();
        }

        _backgroundTimer.Start();
        WatchLinksInBackground();
    }

    private static void LeaveBackground()
    {
        if (!InBackground)
            return;
        InBackground = false;
        _backgroundTimer?.Stop();
        if (Session is null)
            TrayService.Hide();
    }

    /// <summary>In the background, puts the website links back when Octane's launcher took them.</summary>
    private static void WatchLinksInBackground()
    {
        if (!InBackground || !Settings.Prop.RegisterWebsiteProtocol || !InstallerService.IsInstalled(Settings.Prop) || UiShots.Active)
            return;

        try
        {
            if (ProtocolService.CheckLinks().All(link => link.Healthy))
                return;
            ProtocolService.Register(Settings.Prop, State.Prop);
            State.Save();
            Logger.Write("Protocol", "Took the website links back while running in the background.");
        }
        catch (Exception ex)
        {
            Logger.Error("Protocol", ex);
        }
    }

    private void StartupCore(StartupEventArgs e)
    {
        Args = LaunchArgs.Parse(e.Args);
        Native.SetAppUserModelId();
        // -devbase <folder>: developer tests run against a throwaway profile folder instead of the real one.
        var devBase = ArgValue(e.Args, "-devbase");
        Paths.Initialize(devBase ?? InstallerService.PrepareInstallDirectory());
        try { Environment.CurrentDirectory = Paths.Base; } catch { /* keep the process cwd if Windows refuses */ }
        Logger.Initialize();
        Settings = new JsonStore<Settings>(Paths.Settings);
        State = new JsonStore<AppState>(Paths.State);
        // No settings file (and no backup) yet means this run creates the profile: a new user.
        FreshProfile = !File.Exists(Paths.Settings) && !File.Exists(Paths.Settings + ".bak");
        Settings.Load();
        State.Load();
        if (FreshProfile)
        {
            Settings.Prop.SetupPending = true;
            Settings.Save();
        }
        Logger.Write("App", $"Args: {string.Join(' ', e.Args.Select(RedactArg))}");

        // -quit: asks a copy running in the background (or with the menu open) to exit, then exits itself.
        if (e.Args.Any(arg => arg.Equals("-quit", StringComparison.OrdinalIgnoreCase)))
        {
            SignalEvent(MenuQuitEventName);
            Shutdown();
            return;
        }

        if (!string.IsNullOrWhiteSpace(Settings.Prop.InstallLocation) &&
            !Paths.IsLegacyDefault(Settings.Prop.InstallLocation) &&
            !string.Equals(Path.GetFullPath(Settings.Prop.InstallLocation), Path.GetFullPath(Paths.Base), StringComparison.OrdinalIgnoreCase))
            Paths.Initialize(Settings.Prop.InstallLocation);

        if (Paths.IsLegacyDefault(Settings.Prop.InstallLocation) || string.IsNullOrWhiteSpace(Settings.Prop.InstallLocation))
        {
            Settings.Prop.InstallLocation = Paths.Base;
            Settings.Save();
        }

        if (FastFlagService.MigratePresets(Settings.Prop))
            Settings.Save();

        ThemeService.Apply(Settings.Prop.Theme, Settings.Prop.UiStyle);

        // Developer screenshot mode (see UiShots): render the UI to PNGs and exit, nothing else runs.
        var shots = Array.FindIndex(e.Args, arg => arg.Equals("-uishots", StringComparison.OrdinalIgnoreCase));
        var toggle = Array.FindIndex(e.Args, arg => arg.Equals("-uitoggle", StringComparison.OrdinalIgnoreCase));
        var rpcTest = Array.FindIndex(e.Args, arg => arg.Equals("-rpctest", StringComparison.OrdinalIgnoreCase));
        if (rpcTest >= 0)
        {
            var file = rpcTest + 1 < e.Args.Length ? e.Args[rpcTest + 1] : Path.Combine(Paths.Base, "rpctest.txt");
            var hold = rpcTest + 2 < e.Args.Length && int.TryParse(e.Args[rpcTest + 2], out var secs) ? Math.Clamp(secs, 1, 120) : 5;
            _ = RunPresenceTestAsync(file, hold);
            return;
        }

        var devTest = Array.FindIndex(e.Args, arg => arg.Equals("-devtest", StringComparison.OrdinalIgnoreCase));
        if (devTest >= 0 && devTest + 2 < e.Args.Length)
        {
            _ = DevTests.RunAsync(e.Args[devTest + 1], e.Args[devTest + 2], e.Args);
            return;
        }

        if (toggle >= 0)
        {
            var persist = Array.FindIndex(e.Args, arg => arg.Equals("-persist", StringComparison.OrdinalIgnoreCase));
            UiStyle? persistStyle = persist >= 0 && persist + 1 < e.Args.Length && Enum.TryParse<UiStyle>(e.Args[persist + 1], true, out var parsed)
                ? parsed
                : null;
            _ = UiShots.RunToggleTestAsync(
                toggle + 1 < e.Args.Length && !e.Args[toggle + 1].StartsWith('-') ? e.Args[toggle + 1] : Path.Combine(Paths.Base, "ui-toggle"),
                e.Args.Any(arg => arg.Equals("-realmouse", StringComparison.OrdinalIgnoreCase)),
                persistStyle);
            return;
        }

        if (shots >= 0)
        {
            _ = UiShots.RunAsync(shots + 1 < e.Args.Length ? e.Args[shots + 1] : Path.Combine(Paths.Base, "ui-shots"));
            return;
        }

        TrackVersion();

        try
        {
            var client = ClientLocator.Find(Settings.Prop, State.Prop);
            if (client is not null)
            {
                FastFlagService.ApplyAll(Settings.Prop, State.Prop, client, log: true);
                // Persist WrittenFlagKeys so the next launch can remove flags you turned off.
                Save();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("FastFlags", ex);
        }

        if (File.Exists(Paths.Executable))
        {
            Settings.Prop.Installed = true;
            Settings.Prop.InstallLocation = Paths.Base;
        }

        if (InstallerService.IsInstalled(Settings.Prop))
        {
            try
            {
                // Only refresh the installed copy from a newer build (never downgrade it from an old
                // Desktop copy or a stale download).
                if (!string.IsNullOrWhiteSpace(Environment.ProcessPath) &&
                    InstallerService.ShouldRefreshPayload(Environment.ProcessPath, Paths.Executable))
                    InstallerService.CopyPayload(Environment.ProcessPath, Paths.Executable);
                InstallerService.EnsureInstallCasing();
                InstallerService.StripLegacyBinaries(Paths.Base);
                InstallerService.StripLegacyBinaries(Paths.LegacyBase);
                InstallerService.StripLegacyBinaries(Paths.PreviousBase);
                ShortcutService.Publish();
                WindowsAppRegistration.Register();
                if (Settings.Prop.RegisterWebsiteProtocol)
                    ProtocolService.Register(Settings.Prop, State.Prop);
                else
                    ProtocolService.CleanupLegacy(State.Prop);
                Native.NotifyShell();
            }
            catch (Exception ex)
            {
                Logger.Error("App", ex);
            }
        }

        if (Args.Mode == LaunchMode.Uninstall)
        {
            if (Args.Quiet)
            {
                InstallerService.Uninstall(Settings.Prop, State.Prop);
                Settings.Save();
                Shutdown();
                return;
            }

            new UninstallWindow().Show();
            return;
        }

        if (Args.Mode == LaunchMode.ImportMods && !string.IsNullOrWhiteSpace(Args.ImportModsPath))
        {
            var count = ModService.ImportFolder(Args.ImportModsPath);
            Logger.Write("App", $"Imported {count} mod file(s) from {Args.ImportModsPath}");
            Shutdown();
            return;
        }

        if (Args.SkipUpdate)
        {
            var notice = State.Prop.PendingUpdateNotice;
            NotifyService.Show(AppInfo.Name, string.IsNullOrWhiteSpace(notice)
                ? $"Updated to {AppInfo.Version}."
                : notice);
            State.Prop.PendingUpdateNotice = null;
            State.Save();
        }

        if (!InstallerService.IsInstalled(Settings.Prop) && Args.Mode == LaunchMode.Menu)
        {
            new InstallerWindow().Show();
            return;
        }

        if (Args.Mode is LaunchMode.Player or LaunchMode.Studio)
        {
            new BootstrapperWindow(Args).Show();
            return;
        }

        ShowMenu();
        if (_mutex is null)
        {
            // Another process already has the menu open and was asked to show it.
            Shutdown();
            return;
        }

        if (!Args.SkipUpdate)
            _ = AppUpdateService.CheckInBackgroundAsync(Args);
    }

    /// <summary>True when this run created the settings file (a new user): first-run setup, no What's new.</summary>
    public static bool FreshProfile { get; private set; }

    private static string? ArgValue(string[] args, string name)
    {
        var index = Array.FindIndex(args, arg => arg.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>
    /// Notices an update: the last version that ran is older than this one (or missing, for 2.2.1 and older
    /// with an existing profile). Then What's new is due once for this version, next time the menu opens.
    /// </summary>
    public static void TrackVersion()
    {
        var state = State.Prop;
        var previous = state.LastRunVersion;
        if (previous is not null && AppUpdateService.SameVersion(previous, AppInfo.Version))
            return;

        var upgraded = previous is null ? !FreshProfile : AppUpdateService.IsNewer(AppInfo.Version, previous);
        if (upgraded && !string.Equals(state.WhatsNewShownVersion, AppInfo.Version, StringComparison.OrdinalIgnoreCase))
            state.WhatsNewPendingVersion = AppInfo.Version;
        Logger.Write("App", previous is null
            ? $"First run of {AppInfo.Version}{(FreshProfile ? " (new profile)" : " (updated from 2.2.1 or older)")}"
            : $"Version changed {previous} -> {AppInfo.Version}");
        state.LastRunVersion = AppInfo.Version;
        State.Save();
    }

    /// <summary>What's new should open with the menu: an update was noticed and it wasn't shown or turned off.</summary>
    public static bool WhatsNewDue =>
        Settings.Prop.ShowWhatsNew &&
        string.Equals(State.Prop.WhatsNewPendingVersion, AppInfo.Version, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(State.Prop.WhatsNewShownVersion, AppInfo.Version, StringComparison.OrdinalIgnoreCase);

    public static void MarkWhatsNewShown()
    {
        State.Prop.WhatsNewShownVersion = AppInfo.Version;
        State.Prop.WhatsNewPendingVersion = null;
        Save();
    }

    private static string RedactArg(string arg)
    {
        if (!LaunchArgs.IsProtocol(arg))
            return arg;

        // With activity tracking off, the log doesn't record which game was joined.
        if (Settings?.Prop.ActivityTracking != true)
            return "[protocol]";

        var parsed = ProtocolPayload.TryParse(arg);
        return parsed?.PlaceLauncherUrl is null
            ? "[protocol]"
            : $"[protocol launchmode={parsed.LaunchMode} {parsed.PlaceLauncherUrl}]";
    }

    public static void Save()
    {
        if (UiShots.Active && !UiShots.AllowSave)
            return;

        Settings.Save();
        State.Save();
    }


    public static void LaunchOctane(LaunchMode mode = LaunchMode.Player)
    {
        new BootstrapperWindow(new LaunchArgs { Mode = mode }).Show();
    }

    private const string MenuMutexName = "XBootstrapperMenu";
    private const string MenuShowEventName = "XBootstrapperMenu.Show";
    private const string MenuQuitEventName = "XBootstrapperMenu.Quit";

    /// <summary>
    /// Opens the settings menu. Only one menu exists across processes: if another process already
    /// has it, that window is brought to the front instead.
    /// </summary>
    public static void ShowMenu()
    {
        var app = (App)Current;
        if (app._mutex is null)
        {
            var mutex = new Mutex(true, MenuMutexName, out var created);
            if (!created)
            {
                mutex.Dispose();
                SignalExistingMenu();
                return;
            }

            app._mutex = mutex;
            app.ListenForMenuRequests();
        }

        LeaveBackground();
        var existing = Current.Windows.OfType<MenuWindow>().FirstOrDefault();
        if (existing is not null)
        {
            existing.Show();
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }

        new MenuWindow().Show();
    }

    private static void SignalExistingMenu() => SignalEvent(MenuShowEventName);

    private static void SignalEvent(string name)
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(name, out var handle))
            {
                using (handle)
                    handle.Set();
            }
        }
        catch (Exception ex)
        {
            Logger.Write("App", $"Could not reach the open menu: {ex.Message}");
        }
    }

    private void ListenForMenuRequests()
    {
        Listen(MenuShowEventName, ShowMenu);
        Listen(MenuQuitEventName, () =>
        {
            Logger.Write("App", "Asked to exit by another X Bootstrapper process (-quit).");
            ExitFromTray();
        });
    }

    private void Listen(string eventName, Action action)
    {
        try
        {
            var handle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            var thread = new Thread(() =>
            {
                while (true)
                {
                    handle.WaitOne();
                    Dispatcher.BeginInvoke(action);
                }
            })
            {
                IsBackground = true,
                Name = eventName + " listener"
            };
            thread.Start();
        }
        catch (Exception ex)
        {
            Logger.Write("App", $"Menu listener unavailable: {ex.Message}");
        }
    }

    /// <summary>Keeps the process alive in the background until the launched client closes.</summary>
    public static void StartSession(GameSession session)
    {
        // Only end a previous session: the Discord connection opened at launch (PrepareDiscord) must survive
        // into this one, or Octane's own status connects first and wins (2.2.1 dropped and reopened it here).
        if (Session is not null)
            EndSession();
        if (InBackground)
        {
            InBackground = false;
            _backgroundTimer?.Stop();
        }

        Session = session;
        session.Ended += () =>
        {
            if (ReferenceEquals(Session, session))
                EndSession();
        };

        var s = Settings.Prop;
        if (s.ShowTrayIcon)
            TrayService.Show(session.IsStudio ? "Octane Studio is open" : "Octane is running");

        IntegrationService.Start(s.Integrations);

        if (s.EffectiveDiscordStatus == DiscordStatusMode.XBootstrapper)
            _ = StartPresenceAsync(session);
        else
            Logger.Write("Discord", DescribeStatusChoice(s.EffectiveDiscordStatus));

        // We hand the join link to OctanePlayerLauncher.exe, which registers itself for octane-player://
        // and octane-studio:// again when it runs; the next Play on octane.wtf then skipped X Bootstrapper
        // (no presence, no FastFlags). Take the links back once the client is up.
        // With "Keep running in the background" off, stay only as long as something needs the game: X Bootstrapper's
        // Discord status or programs to close with the game hold the whole session; otherwise only the link retake.
        var holdWholeSession = NeedsWholeSession(s);
        Logger.Write("Session", holdWholeSession
            ? "Staying until the game closes (" + WhyHold(s) + ")."
            : "Nothing needs the whole game session; exiting after the website links are taken back.");
        _ = ReclaimProtocolsAsync(session, detach: !holdWholeSession);
    }

    public static bool NeedsWholeSession(Settings s) =>
        s.KeepRunningInBackground ||
        s.EffectiveDiscordStatus == DiscordStatusMode.XBootstrapper ||
        s.Integrations.Any(item => item.Enabled && item.AutoClose && !string.IsNullOrWhiteSpace(item.Path));

    private static string WhyHold(Settings s)
    {
        var reasons = new List<string>();
        if (s.EffectiveDiscordStatus == DiscordStatusMode.XBootstrapper) reasons.Add("X Bootstrapper's Discord status");
        if (s.Integrations.Any(item => item.Enabled && item.AutoClose && !string.IsNullOrWhiteSpace(item.Path))) reasons.Add("programs to close with the game");
        if (s.KeepRunningInBackground) reasons.Add("keep running in the background is on");
        return string.Join(", ", reasons);
    }

    /// <summary>Seconds after the client starts before the links are taken back (the launcher re-registers them as it runs).</summary>
    internal static TimeSpan ReclaimDelay { get; set; } = TimeSpan.FromSeconds(15);

    private static async Task ReclaimProtocolsAsync(GameSession session, bool detach)
    {
        await Task.Delay(ReclaimDelay);
        if (!ReferenceEquals(Session, session))
            return;

        ReclaimProtocols();
        if (detach)
        {
            // The game keeps running; this process just stops waiting for it.
            Logger.Write("Session", "Links taken back; X Bootstrapper exits while Octane keeps running.");
            EndSession();
        }
    }

    private static void ReclaimProtocols()
    {
        if (!Settings.Prop.RegisterWebsiteProtocol || UiShots.Active)
            return;

        try
        {
            ProtocolService.Register(Settings.Prop, State.Prop);
            State.Save();
        }
        catch (Exception ex)
        {
            Logger.Error("Protocol", ex);
        }
    }

    private static readonly TimeSpan PresenceRetry = TimeSpan.FromSeconds(20);
    private const int PresenceMaxFailures = 15;
    private static readonly SemaphoreSlim DiscordGate = new(1, 1);

    /// <summary>
    /// Opens the Rich Presence connection as soon as a launch starts, before the client runs.
    /// Discord shows one local Rich Presence at a time and keeps the connection that came first; the Octane
    /// client opens its own a moment after it starts, so connecting only once the client was running (as
    /// before 2.2.1) meant X Bootstrapper's presence was accepted but never displayed.
    /// </summary>
    public static void PrepareDiscord(bool studio)
    {
        if (UiShots.Active && !UiShots.AllowDiscord)
            return;
        if (Settings.Prop.EffectiveDiscordStatus != DiscordStatusMode.XBootstrapper)
        {
            Logger.Write("Discord", DescribeStatusChoice(Settings.Prop.EffectiveDiscordStatus));
            return;
        }

        _ = PrepareDiscordAsync(studio);
    }

    private static async Task PrepareDiscordAsync(bool studio)
    {
        try
        {
            var discord = await EnsureDiscordAsync();
            if (discord is null || Session is not null)
                return;

            discord.SetPresence(studio ? "Starting Octane Studio" : "Starting Octane", null, DateTimeOffset.UtcNow);
            // The launch window ends in a session (which takes the connection over) or not at all.
            await Task.Delay(TimeSpan.FromMinutes(3));
            if (Session is null && ReferenceEquals(Discord, discord))
                ReleaseDiscord();
        }
        catch (Exception ex)
        {
            Logger.Write("Discord", $"Rich Presence failed: {ex.Message}");
        }
    }

    private static string DescribeStatusChoice(DiscordStatusMode mode) => mode switch
    {
        DiscordStatusMode.Octane => "Discord status is set to Octane's own: X Bootstrapper does not connect, so the Octane client's status shows.",
        DiscordStatusMode.None => "Discord status is set to None: X Bootstrapper does not connect (the Octane client may still set its own).",
        _ => "Discord status: X Bootstrapper"
    };

    /// <summary>Drops a connection opened for a launch that was cancelled or failed.</summary>
    public static void ReleaseDiscord()
    {
        if (Session is not null)
            return;

        Discord?.Dispose();
        Discord = null;
    }

    /// <summary>Returns the live connection, connecting (READY handshake) if there is none. One at a time.</summary>
    private static async Task<DiscordService?> EnsureDiscordAsync()
    {
        var clientId = Settings.Prop.EffectiveDiscordClientId;
        if (!clientId.All(char.IsDigit))
        {
            Logger.Write("Discord", "The custom Discord application ID is not a number; Rich Presence is off.");
            return null;
        }

        await DiscordGate.WaitAsync();
        try
        {
            if (Discord is { Connected: true } live)
                return live;

            Discord?.Dispose();
            Discord = await Task.Run(() => DiscordService.ConnectAsync(clientId));
            return Discord;
        }
        finally
        {
            DiscordGate.Release();
        }
    }

    /// <summary>
    /// Keeps Rich Presence up for the whole session: reuses the connection opened at launch (or connects),
    /// shows the session, and reconnects if Discord starts later or restarts. Cleared when the session ends.
    /// Place titles are resolved from Octane so Discord shows the game name, not only "Place 52645".
    /// </summary>
    private static async Task StartPresenceAsync(GameSession session)
    {
        var failures = 0;
        DiscordService? shownOn = null;
        string? shownState = null;
        Task<string?>? nameLookup = null;
        if (session.PlaceId is not null && Settings.Prop.ActivityTracking)
            nameLookup = PlaceInfoService.ResolveNameAsync(session.PlaceId);

        while (ReferenceEquals(Session, session) && failures < PresenceMaxFailures)
        {
            try
            {
                if (nameLookup is not null)
                {
                    // Don't wait the full reconnect interval for the title; refresh Discord as soon as it arrives.
                    var finished = await Task.WhenAny(nameLookup, Task.Delay(TimeSpan.FromSeconds(2)));
                    if (finished == nameLookup)
                    {
                        try
                        {
                            session.PlaceName = await nameLookup;
                        }
                        catch (Exception ex)
                        {
                            Logger.Write("Discord", $"Place name lookup failed: {ex.Message}");
                        }

                        nameLookup = null;
                    }
                }

                var discord = await EnsureDiscordAsync();
                if (!ReferenceEquals(Session, session))
                    return;

                if (discord is null)
                {
                    failures++;
                }
                else
                {
                    failures = 0;
                    var state = PresenceState(session);
                    if (!ReferenceEquals(discord, shownOn) || !string.Equals(state, shownState, StringComparison.Ordinal))
                    {
                        shownOn = discord;
                        shownState = state;
                        discord.SetPresence(
                            session.IsStudio ? "Building in Octane Studio" : "Playing on Octane",
                            state,
                            session.Started);
                    }
                }
            }
            catch (Exception ex)
            {
                failures++;
                Logger.Write("Discord", $"Rich Presence failed: {ex.Message}");
            }

            // Poll often while the place title is still loading; otherwise the usual reconnect interval.
            await Task.Delay(nameLookup is null ? PresenceRetry : TimeSpan.FromSeconds(1));
        }
    }

    private static string PresenceState(GameSession session)
    {
        var s = Settings.Prop;
        if (!s.ActivityTracking)
            return session.IsStudio ? "Octane Studio" : "In game";

        if (!string.IsNullOrWhiteSpace(session.PlaceName))
            return PlaceInfoService.TruncateForDiscord(session.PlaceName);

        if (!string.IsNullOrWhiteSpace(session.PlaceId))
            return $"Place {session.PlaceId}";

        return session.IsStudio ? "Octane Studio" : "In game";
    }

    /// <summary>
    /// <c>-rpctest &lt;result file&gt; [seconds]</c>: connects, shows a presence for a few seconds, clears it,
    /// and writes Discord's replies to the file. Nothing else runs (no game, no menu).
    /// </summary>
    private static async Task RunPresenceTestAsync(string file, int seconds)
    {
        var lines = new List<string>();
        var ok = false;
        try
        {
            var discord = await EnsureDiscordAsync();
            if (discord is null)
            {
                lines.Add("FAIL: could not connect to Discord (is it running?)");
            }
            else
            {
                lines.Add("connected (READY received)");
                var reply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                discord.ActivityReply += json => reply.TrySetResult(json);
                discord.SetPresence("Playing on Octane", "In game", DateTimeOffset.UtcNow);
                var answer = await Task.WhenAny(reply.Task, Task.Delay(5000)) == reply.Task ? reply.Task.Result : null;
                lines.Add("SET_ACTIVITY reply: " + (answer ?? "(none within 5 s)"));
                ok = answer is not null && !answer.Contains("\"evt\":\"ERROR\"", StringComparison.Ordinal) &&
                     answer.Contains("\"large_image\"", StringComparison.Ordinal);
                await Task.Delay(TimeSpan.FromSeconds(seconds));

                var cleared = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                discord.ActivityReply += json => cleared.TrySetResult(json);
                discord.Clear();
                var clearAnswer = await Task.WhenAny(cleared.Task, Task.Delay(5000)) == cleared.Task ? cleared.Task.Result : null;
                lines.Add("clear reply: " + (clearAnswer ?? "(none within 5 s)"));
                ok &= clearAnswer?.Contains("\"data\":null", StringComparison.Ordinal) == true;
                Discord = null;
                discord.Dispose();
            }
        }
        catch (Exception ex)
        {
            lines.Add("EXCEPTION " + ex);
        }

        lines.Add(ok ? "RESULT: presence accepted (with image) and cleared" : "RESULT: FAIL");
        File.WriteAllLines(file, lines);
        Current.Shutdown(ok ? 0 : 1);
    }

    public static void EndSession()
    {
        var session = Session;
        Session = null;
        Discord?.Dispose();
        Discord = null;
        if (session is not null)
        {
            IntegrationService.StopAll();
            TrayService.Hide();
            ReclaimProtocols();
        }

        session?.Dispose();
        if (session is not null)
            RequestExitIfIdle();
    }

    /// <summary>Tray "Exit": stop waiting for the game and quit (the game itself keeps running).</summary>
    public static void ExitFromTray()
    {
        EndSession();
        Current?.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            EndSession();
        }
        catch (Exception ex)
        {
            Logger.Error("App", ex);
        }

        TrayService.Hide();
        NotifyService.Dispose();
        if (!SuppressSave)
            Save();
        Logger.Close();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

