using System.Diagnostics;
using Caelus.Core;
using Caelus.Models;

namespace Caelus.Services;

public sealed record LaunchResult(Process? Process, bool IsGame, ClientInstall Install);

public sealed class BootstrapperService
{
    private static readonly TimeSpan ClientStartTimeout = TimeSpan.FromSeconds(45);

    private readonly Settings _settings;
    private readonly AppState _state;
    private readonly LaunchArgs _args;

    public event Action<string>? StatusChanged;
    public event Action<double, bool>? ProgressChanged;

    public BootstrapperService(Settings settings, AppState state, LaunchArgs args)
    {
        _settings = settings;
        _state = state;
        _args = args;
    }

    public async Task<LaunchResult> RunAsync(CancellationToken token)
    {
        SetStatus("Looking for Octane...");
        SetProgress(0, indeterminate: true);

        // File-system work runs off the UI thread so the launch window stays responsive.
        var install = await Task.Run(() => ClientLocator.Find(_settings, _state), token);
        if (install is null)
            throw new InvalidOperationException(
                "X Bootstrapper could not find an Octane client.\n\n" +
                "Install the official Octane launcher from octane.wtf once, or set a client folder in Install settings.");

        SetStatus("Applying FastFlags and mods...");
        await Task.Run(() => ApplyOverrides(install, log: true, mods: true), token);

        if (_settings.RegisterWebsiteProtocol)
            await Task.Run(() => ProtocolService.Register(_settings, _state), token);

        SetStatus(_args.Mode == LaunchMode.Studio
            ? "Starting Octane Studio..."
            : WantsAppBeta() ? "Starting Octane App..." : "Starting Octane...");
        var result = await LaunchAsync(install, token);

        _state.PlayerVersionGuid = install.VersionGuid;
        _state.PlayerExecutable = install.PlayerExecutable;
        _state.StudioExecutable = install.StudioExecutable;
        _state.LastLaunched = DateTime.Now;

        Logger.Write("Bootstrapper", result.Process is null ? "Launch handed off" : $"Launched {Describe(result.Process)}");
        return result;
    }

    private async Task<LaunchResult> LaunchAsync(ClientInstall install, CancellationToken token)
    {
        var studio = _args.Mode == LaunchMode.Studio;
        var watchNames = studio ? ClientLocator.StudioProcessNames : ClientLocator.PlayerProcessNames;
        var alreadyRunning = ClientLocator.RunningIds(watchNames);

        // App Beta is a direct player launch with --app. OctanePlayerLauncher does not understand
        // launchmode:app (it only builds join flags), and handing those URIs off is what produces
        // the white screen people see with the DevForum method on Octane.
        if (!studio && WantsAppBeta())
        {
            if (!AppBetaService.ClientSupportsAppBeta(install.PlayerExecutable))
                throw new InvalidOperationException(AppBetaService.MissingSupportMessage(install.PlayerExecutable));

            Logger.Write("Bootstrapper", $"Launching App Beta: {Path.GetFileName(install.PlayerExecutable)} {AppBetaService.LaunchArguments}");
            SetStatus("Starting Octane App...");
            var app = StartProcess(install.PlayerExecutable, start => start.Arguments = AppBetaService.LaunchArguments);
            return new LaunchResult(app, true, install);
        }

        if (!string.IsNullOrWhiteSpace(_args.ProtocolUri))
        {
            // OctanePlayer does not accept octane-player: URIs directly (error 610). The official
            // launcher turns the website link into a join, so the link goes to the official handler
            // for its scheme (octane-player -> player, octane-studio -> Studio).
            var scheme = _args.TargetScheme ?? "octane-player";
            var uri = _args.ToLaunchUri();
            var handler = ProtocolService.OfficialHandler(scheme, uri, install, _state);
            if (handler is null)
                throw new InvalidOperationException(
                    "OctanePlayerLauncher.exe is missing, so the website link cannot be turned into a join.\n\n" +
                    "Install the official Octane launcher once, then try again.");

            Logger.Write("Bootstrapper", $"Handing the {scheme} link to {Path.GetFileName(handler.Value.Exe)}.");
            var launcher = StartProcess(handler.Value.Exe, start => start.Arguments = handler.Value.Arguments);

            SetStatus(studio ? "Waiting for Octane Studio..." : "Waiting for Octane...");
            var game = await ClientLocator.WaitForNewProcessAsync(watchNames, alreadyRunning, ClientStartTimeout, token);
            if (game is null)
            {
                Logger.Write("Bootstrapper", $"No new {(studio ? "Studio" : "player")} process appeared after the handoff.");
                return new LaunchResult(launcher, false, install);
            }

            launcher.Dispose();
            if (!studio)
                await Task.Run(() => ApplyToRunningPlayer(game), token);
            return new LaunchResult(game, true, install);
        }

        var exe = studio
            ? install.StudioExecutable ?? throw new InvalidOperationException("Octane Studio was not found next to the client.")
            : install.PlayerExecutable;

        var process = StartProcess(exe, _ => { });
        return new LaunchResult(process, true, install);
    }

    /// <summary>
    /// App Beta from <c>-app</c>, a launchmode:app protocol, or Behaviour → Launch App Beta
    /// (only when this launch is not already a website join).
    /// </summary>
    private bool WantsAppBeta()
    {
        if (_args.Mode == LaunchMode.Studio)
            return false;
        if (_args.AppBeta || AppBetaService.IsAppModeProtocol(_args.ProtocolUri))
            return true;
        // A real join URI must still go to OctanePlayerLauncher; App Beta is for opening the home UI.
        if (!string.IsNullOrWhiteSpace(_args.ProtocolUri))
            return false;
        return _settings.LaunchAppBeta;
    }

    /// <summary>
    /// The official launcher can rewrite ClientSettings as it starts the player, so write the flags
    /// once more after the player shows up (once, not in a loop).
    /// </summary>
    private void ApplyToRunningPlayer(Process player)
    {
        try
        {
            Logger.Write("Bootstrapper", $"OctanePlayer is running ({player.Id})");
            var target = ClientLocator.FromPlayerProcess(player) ?? ClientLocator.Find(_settings, _state);
            FastFlagService.ApplyAll(_settings, _state, target, log: true);
        }
        catch (Exception ex)
        {
            Logger.Write("Bootstrapper", $"Could not re-apply FastFlags: {ex.Message}");
        }
    }

    private void ApplyOverrides(ClientInstall install, bool log, bool mods)
    {
        try
        {
            FastFlagService.ApplyAll(_settings, _state, install, log);
        }
        catch (Exception ex)
        {
            Logger.Write("Bootstrapper", $"Could not apply FastFlags: {ex.Message}");
        }

        if (!mods)
            return;

        try
        {
            ModService.Apply(install, log);
        }
        catch (Exception ex)
        {
            Logger.Write("Bootstrapper", $"Could not apply mods: {ex.Message}");
        }
    }

    private static Process StartProcess(string exe, Action<ProcessStartInfo> configure)
    {
        if (!File.Exists(exe))
            throw new FileNotFoundException("The Octane executable is missing.", exe);

        var start = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = Path.GetDirectoryName(exe),
            UseShellExecute = false
        };
        configure(start);

        var process = Process.Start(start);
        if (process is null)
            throw new InvalidOperationException("Windows refused to start the Octane client.");

        return process;
    }

    private static string Describe(Process process)
    {
        try
        {
            return $"{process.ProcessName}.exe ({process.Id})";
        }
        catch (Exception ex)
        {
            return $"pid {process.Id} ({ex.Message})";
        }
    }

    private void SetStatus(string status)
    {
        Logger.Write("Bootstrapper", status);
        StatusChanged?.Invoke(status);
    }

    private void SetProgress(double value, bool indeterminate)
    {
        ProgressChanged?.Invoke(value, indeterminate);
    }
}
