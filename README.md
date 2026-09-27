# X Bootstrapper

Open-source launcher for [Octane](https://octane.wtf), the 2021 Roblox revival.

X Bootstrapper does not ship a game client. It finds your Octane install (`%LOCALAPPDATA%\Octane`), writes FastFlags, copies your mods into the 2021 client folder, then starts Octane Player or Studio.

**Not affiliated with Roblox Corporation or the Octane team.**

## Install

1. Download **X.Bootstrapper.Setup.exe** from [Releases](https://github.com/nicolasishere1282-dotcom/x-bootstrapper/releases/latest).
2. Run it. You don't need an administrator account. It installs to `%LOCALAPPDATA%\X Bootstrapper`.

You need Octane installed from [octane.wtf](https://octane.wtf) first. X Bootstrapper uses the 2021 client at `%LOCALAPPDATA%\Octane\clients\2021\OctanePlayer.exe` and Studio at `%LOCALAPPDATA%\Octane\Studio\2021\RobloxStudioBeta.exe`.

## Updates

X Bootstrapper checks GitHub Releases for this repository when you open it. It installs new versions automatically, and you can turn that off. The Install tab lists every release, so you can stay on your current version or install an older one.

## Features

- Handles `octane-player://` and `octane-studio://`, so **Play** on octane.wtf opens through X Bootstrapper
- Optional `roblox-player://` handler
- Mods, FastFlags and renderer presets, applied to the Octane 2021 client
- Launcher themes
- Windows notifications when a game finishes loading and when a new launcher version is out
- Discord Rich Presence over local IPC

## Build

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet build "src\Caelus\Caelus.csproj" -c Release
```

To build the self-contained app and the installer:

```powershell
.\publish.ps1
```

`publish.ps1` publishes a self-contained win-x64 build, then compiles `installer\XBootstrapper.iss` with [Inno Setup 6](https://jrsoftware.org/isinfo.php). It downloads Inno Setup if it can't find it. The installer is written to `dist\X Bootstrapper Setup.exe`.

Releases are built by `.github/workflows/release.yml` when a `v*` tag is pushed. That workflow publishes a single-file exe, builds the installer and attaches `X.Bootstrapper.Setup.exe` to the GitHub release.

## Usage

- The first run of a portable build opens the installer.
- The Start Menu shortcut opens settings (`-menu`).
- `-player` launches Octane. `-studio` launches Octane Studio.
- A protocol link from octane.wtf is handed to the Octane 2021 client.

## License

MIT. See [LICENSE](LICENSE).