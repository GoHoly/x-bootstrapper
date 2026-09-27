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
            app.Shutdown();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void StartupCore(StartupEventArgs e)
    {
        Args = LaunchArgs.Parse(e.Args);
        Native.SetAppUserModelId();
        Paths.Initialize(InstallerService.PrepareInstallDirectory());
        try { Environment.CurrentDirectory = Paths.Base; } catch { /* keep the process cwd if Windows refuses */ }
        Logger.Initialize();
        Settings = new JsonStore<Settings>(Paths.Settings);
        State = new JsonStore<AppState>(Paths.State);
        Settings.Load();
        State.Load();
        Logger.Write("App", $"Args: {string.Join(' ', e.Args.Select(RedactArg))}");

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

        try
        {
            var client = ClientLocator.Find(Settings.Prop, State.Prop);
            if (client is not null)
                FastFlagService.ApplyAll(Settings.Prop, State.Prop, client, log: true);
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

    private static void SignalExistingMenu()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(MenuShowEventName, out var handle))
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
        try
        {
            var handle = new EventWaitHandle(false, EventResetMode.AutoReset, MenuShowEventName);
            var thread = new Thread(() =>
            {
                while (true)
                {
                    handle.WaitOne();
                    Dispatcher.BeginInvoke(ShowMenu);
                }
            })
            {
                IsBackground = true,
                Name = "MenuShowListener"
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
        EndSession();
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

        if (s.DiscordRichPresence)
            _ = StartPresenceAsync(session);
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
        if (!Settings.Prop.DiscordRichPresence || UiShots.Active)
            return;

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
    /// </summary>
    private static async Task StartPresenceAsync(GameSession session)
    {
        var failures = 0;
        DiscordService? shownOn = null;
        while (ReferenceEquals(Session, session) && failures < PresenceMaxFailures)
        {
            try
            {
                var discord = await EnsureDiscordAsync();
                if (!ReferenceEquals(Session, session))
                    return;

                if (discord is null)
                {
                    failures++;
                }
                else if (!ReferenceEquals(discord, shownOn))
                {
                    failures = 0;
                    shownOn = discord;
                    var s = Settings.Prop;
                    // With activity tracking off, the place is never shared.
                    var place = s.ActivityTracking ? session.PlaceId : null;
                    discord.SetPresence(
                        session.IsStudio ? "Building in Octane Studio" : "Playing on Octane",
                        place is null ? (session.IsStudio ? "Octane Studio" : "In game") : $"Place {place}",
                        session.Started);
                }
            }
            catch (Exception ex)
            {
                failures++;
                Logger.Write("Discord", $"Rich Presence failed: {ex.Message}");
            }

            await Task.Delay(PresenceRetry);
        }
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

