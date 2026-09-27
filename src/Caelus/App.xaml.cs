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

        ThemeService.Apply(Settings.Prop.Theme);

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

        if (Settings.Prop.DiscordRichPresence && !string.IsNullOrWhiteSpace(Settings.Prop.DiscordClientId))
        {
            Discord = new DiscordService();
            if (Discord.Connect(Settings.Prop.DiscordClientId))
            {
                var place = Settings.Prop.ActivityTracking ? session.PlaceId : null;
                Discord.SetPresence(
                    session.IsStudio ? "Building in Octane Studio" : place is null ? "Playing Octane" : $"Place {place}",
                    "2021 revival",
                    place);
            }
        }
    }

    public static void EndSession()
    {
        var session = Session;
        Session = null;
        Discord?.Dispose();
        Discord = null;
        session?.Dispose();
        if (session is not null)
            RequestExitIfIdle();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Session?.Dispose();
        Discord?.Dispose();
        NotifyService.Dispose();
        if (!SuppressSave)
            Save();
        Logger.Close();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

