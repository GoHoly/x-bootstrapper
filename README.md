# X Bootstrapper

Open-source launcher for [Octane](https://octane.wtf), the 2021 Roblox revival.

X Bootstrapper does not ship a game client. It finds your Octane install (`%LOCALAPPDATA%\Octane`), writes FastFlags, copies your mods into the 2021 client folder, then starts Octane Player or Studio.

**Not affiliated with Roblox Corporation or the Octane team.**

## Install

1. Download **X.Bootstrapper.Setup.exe** from [Releases](https://github.com/nicolasishere1282-dotcom/x-bootstrapper/releases/latest).
2. Run it. You don't need an administrator account. It installs to `%LOCALAPPDATA%\X Bootstrapper`.

You need Octane installed from [octane.wtf](https://octane.wtf) first. X Bootstrapper uses the 2021 client at `%LOCALAPPDATA%\Octane\clients\2021\OctanePlayer.exe` and Studio at `%LOCALAPPDATA%\Octane\Studio\2021\RobloxStudioBeta.exe`.

## Updates

X Bootstrapper checks GitHub Releases for this repository when you open it. It installs new versions automatically (never during a launch or while a game is running), and you can turn that off. Updates show a progress window you can cancel, and each download is checked against the SHA-256 digest GitHub publishes for the release asset. The Install tab lists every release, so you can stay on your current version or install an older one.

## Features

- Handles `octane-player://` and `octane-studio://`, so **Play** on octane.wtf opens through X Bootstrapper. The official launcher's handler is remembered and restored on uninstall
- Mods: import a pack or single files, mod profiles, a list of applied files, and one-click restore of the client's original files (originals are backed up before they are replaced)
- FastFlags: renderer/FPS presets plus an editable flag table with JSON import/export and flag profiles
- Launch with the game: start your own programs with Octane and optionally close them when it exits
- Tray icon while a game is running (Open settings, Open logs, Exit)
- Launcher themes
- Windows notifications when a game is starting and when a new launcher version is out
- Discord Rich Presence over local IPC (see below)

## Discord Rich Presence

Rich Presence is on by default and works without any setup. While a game you started through X Bootstrapper is open (and the Discord desktop app is running), your profile shows **X BootStrapper For Octane** with "Playing on Octane", the Octane artwork, and how long you've been playing. X Bootstrapper talks to Discord only over the local IPC pipe, and it reconnects if you start Discord after the game.

- Turn it off under **Integrations → Discord Rich Presence**.
- With **Activity tracking** off, the place ID is not shown in presence or written to the logs.
- **Custom application ID (optional):** leave the field empty to use the built-in application. To use your own, create one at <https://discord.com/developers/applications>, paste its Application ID into **Integrations**, and upload a Rich Presence art asset named `octane` if you want the image.

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

Releases are built by `.github/workflows/release.yml` when a `v*` tag is pushed. That workflow publishes a single-file exe, builds the installer and attaches `X.Bootstrapper.Setup.exe` and `X.Bootstrapper.exe` to the GitHub release.

## Usage

- The first run of a portable build opens the installer.
- The Start Menu shortcut opens settings (`-menu`).
- `-player` launches Octane. `-studio` launches Octane Studio.
- A protocol link from octane.wtf is handed to the official Octane launcher, which joins the game; X Bootstrapper applies your FastFlags and mods first.
- Uninstalling never deletes the Octane client. Files your mods replaced are restored first.

## License

MIT. See [LICENSE](LICENSE).