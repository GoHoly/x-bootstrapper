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
    public static ProcessWatch? Watch { get; set; }

    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("App", args.Exception);
            System.Windows.MessageBox.Show(args.Exception.Message, AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        Args = LaunchArgs.Parse(e.Args);
        Native.SetAppUserModelId();
        Paths.Initialize(InstallerService.PrepareInstallDirectory());
        Logger.Initialize();
        Logger.Write("App", $"Args: {string.Join(' ', e.Args.Select(RedactArg))}");

        Settings = new JsonStore<Settings>(Paths.Settings);
        State = new JsonStore<AppState>(Paths.State);
        Settings.Load();
        State.Load();

        if (!string.IsNullOrWhiteSpace(Settings.Prop.InstallLocation) &&
            !Paths.IsLegacyDefault(Settings.Prop.InstallLocation) &&
            !string.Equals(Path.GetFullPath(Settings.Prop.InstallLocation), Path.GetFullPath(Paths.Base), StringComparison.OrdinalIgnoreCase))
            Paths.Initialize(Settings.Prop.InstallLocation);

        if (Paths.IsLegacyDefault(Settings.Prop.InstallLocation) || string.IsNullOrWhiteSpace(Settings.Prop.InstallLocation))
        {
            Settings.Prop.InstallLocation = Paths.Base;
            Settings.Save();
        }

        ThemeService.Apply(Settings.Prop.Theme);

        if (File.Exists(Paths.Executable))
        {
            Settings.Prop.Installed = true;
            Settings.Prop.InstallLocation = Paths.Base;
        }

        if (InstallerService.IsInstalled(Settings.Prop))
        {
            try
            {
                if (!File.Exists(Paths.Executable))
                    InstallerService.CopyPayload(Environment.ProcessPath!, Paths.Executable);
                InstallerService.EnsureInstallCasing();
                InstallerService.StripLegacyBinaries(Paths.Base);
                InstallerService.StripLegacyBinaries(Paths.LegacyBase);
                InstallerService.StripLegacyBinaries(Paths.PreviousBase);
                ShortcutService.Publish();
                WindowsAppRegistration.Register();
                if (Settings.Prop.RegisterWebsiteProtocol)
                    ProtocolService.Register(Settings.Prop);
                Native.NotifyShell();
            }
            catch (Exception ex)
            {
                Logger.Error("App", ex);
            }
        }

        if (Args.Mode == LaunchMode.Uninstall)
        {
            InstallerService.Uninstall(Settings.Prop, removeClient: false);
            Settings.Save();
            Shutdown();
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
            NotifyService.Show(AppInfo.Name, $"Updated to {AppInfo.Version}.");

        if (!InstallerService.IsInstalled(Settings.Prop) && Args.Mode == LaunchMode.Menu)
        {
            new InstallerWindow().Show();
            return;
        }

        if (Args.Mode is LaunchMode.Player or LaunchMode.Studio)
        {
            new BootstrapperWindow().Show();
            return;
        }

        _mutex = new Mutex(true, "XBootstrapperMenu", out var created);
        if (!created)
        {
            Shutdown();
            return;
        }

        new MenuWindow().Show();

        if (Settings.Prop.CheckForAppUpdates && !Args.SkipUpdate)
            _ = AppUpdateService.CheckInBackgroundAsync(Args);
    }

    private static string RedactArg(string arg)
    {
        if (!LaunchArgs.IsProtocol(arg))
            return arg;

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

    public static void LaunchAisaka(LaunchMode mode = LaunchMode.Player)
    {
        Args = new LaunchArgs { Mode = mode };
        new BootstrapperWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Discord?.Dispose();
        NotifyService.Dispose();
        Save();
        Logger.Close();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

public sealed class ProcessWatch
{
    public System.Diagnostics.Process Process { get; init; } = null!;
}
