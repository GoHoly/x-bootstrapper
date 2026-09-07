using System.Diagnostics;
using Caelus.Core;
using Caelus.Models;

namespace Caelus.Services;

public sealed class BootstrapperService
{
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

    public async Task<Process?> RunAsync(CancellationToken token)
    {
        SetStatus("Connecting to Aisaka...");
        SetProgress(0, indeterminate: true);

        ClientInstall? install = ClientLocator.Find(_settings, _state);
        var payload = ProtocolPayload.TryParse(_args.ProtocolUri);

        if (_settings.CheckForClientUpdates && payload is null)
        {
            var version = await DeploymentService.QueryAsync(_settings, SetStatus, token);
            if (version is not null)
            {
                var needsInstall = install is null ||
                    !string.Equals(install.VersionGuid, version.VersionGuid, StringComparison.OrdinalIgnoreCase);

                if (needsInstall)
                {
                    var progress = new Progress<double>(value => SetProgress(value, false));
                    var downloaded = await DeploymentService.InstallAsync(_settings, version, progress, SetStatus, token);
                    if (downloaded is not null)
                        install = downloaded;
                }
                else
                {
                    SetStatus("Aisaka is up to date.");
                }
            }
            else
            {
                Logger.Write("Bootstrapper", "No public client manifest was reachable; using a local install if one exists.");
            }
        }

        install ??= ClientLocator.Find(_settings, _state);
        if (install is null)
            throw new InvalidOperationException(
                "X Bootstrapper could not find an Aisaka client.\n\n" +
                "Install the official Aisaka launcher once, or set a client folder / setup URL in Install settings.");

        ApplyOverrides(install, log: true, mods: true);

        if (_settings.RegisterWebsiteProtocol)
            ProtocolService.Register(_settings);

        SetStatus("Starting Aisaka...");
        var process = await LaunchAsync(install, token);

        _state.PlayerVersionGuid = install.VersionGuid;
        _state.PlayerExecutable = install.PlayerExecutable;
        _state.StudioExecutable = install.StudioExecutable;
        _state.LastLaunched = DateTime.Now;

        Logger.Write("Bootstrapper", $"Launched {Describe(process)}");
        return process;
    }

    private async Task<Process> LaunchAsync(ClientInstall install, CancellationToken token)
    {
        // AisakaPlayer does not accept aisaka-player: / caelus-launcher: URIs. That is error 610.
        // The official launcher turns the website Play link into a real join.
        if (!string.IsNullOrWhiteSpace(_args.ProtocolUri))
        {
            if (!string.IsNullOrWhiteSpace(install.LauncherExecutable) && File.Exists(install.LauncherExecutable))
            {
                var join = _args.ToPlayerArgument();
                Logger.Write("Bootstrapper", "Handing the join URI to AisakaLauncher.");
                var launcher = StartProcess(install.LauncherExecutable, start => start.ArgumentList.Add(join));
                var player = await WaitForPlayerAsync(token);
                return player ?? launcher;
            }

            throw new InvalidOperationException(
                "AisakaLauncher.exe is missing, so the website Play link cannot be turned into a join.\n\n" +
                "Install the official Aisaka launcher once, then try Play again.");
        }

        var exe = _args.Mode == LaunchMode.Studio
            ? install.StudioExecutable ?? install.PlayerExecutable
            : install.PlayerExecutable;

        return StartProcess(exe, _ => { });
    }

    private async Task<Process?> WaitForPlayerAsync(CancellationToken token)
    {
        for (var i = 0; i < 300; i++)
        {
            token.ThrowIfCancellationRequested();
            FastFlagService.ApplyAll(_settings, _state, ClientLocator.Find(_settings, _state), log: false);

            var player = ClientLocator.FindRunningPlayer();
            if (player is not null)
            {
                Logger.Write("Bootstrapper", $"AisakaPlayer is running ({player.Id})");
                var target = ClientLocator.FromPlayerProcess(player) ?? ClientLocator.Find(_settings, _state);
                if (target is not null)
                    FastFlagService.ApplyAll(_settings, _state, target, log: true);
                return player;
            }

            await Task.Delay(50, token);
        }

        Logger.Write("Bootstrapper", "AisakaPlayer did not appear after AisakaLauncher started.");
        return null;
    }

    private void ApplyOverrides(ClientInstall install, bool log, bool mods)
    {
        try
        {
            FastFlagService.ApplyAll(_settings, _state, install, log);
            if (mods)
                ModService.Apply(install, log);
        }
        catch (Exception ex)
        {
            Logger.Write("Bootstrapper", $"Could not apply overrides: {ex.Message}");
        }
    }

    private static Process StartProcess(string exe, Action<ProcessStartInfo> configure)
    {
        if (!File.Exists(exe))
            throw new FileNotFoundException("The Aisaka executable is missing.", exe);

        var start = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = Path.GetDirectoryName(exe),
            UseShellExecute = false
        };
        configure(start);

        var process = Process.Start(start);
        if (process is null)
            throw new InvalidOperationException("Windows refused to start the Aisaka client.");

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
