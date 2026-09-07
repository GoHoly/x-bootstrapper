# X Bootstrapper

Open-source launcher for [Aisaka](https://www.aisaka.me/), the 2021 Roblox revival.

X Bootstrapper does not ship a client. It finds the installed Aisaka player, writes FastFlags, copies your mods onto the version folder, then starts Player or Studio.

**Not affiliated with Roblox Corporation.**

## Install

Download **X Bootstrapper Setup.exe** from [Releases](https://github.com/nicolasishere1282-dotcom/x-bootstrapper/releases/latest). No administrator account is required. It installs to `%LOCALAPPDATA%\X Bootstrapper`.

The app auto-updates from GitHub Releases when you open the menu.

## Features

- Mods, Fast Flags, and launcher themes (including xyxy light/dark)
- Windows notifications when a game or Aisaka finishes loading
- Auto-update from this repository
- `aisaka-player://` / `aisaka-launcher://` handlers for Play on aisaka.me
- Optional `roblox-player://` handler
- Discord Rich Presence over local IPC
- Multi-instance (holds `ROBLOX_singletonEvent`)

## Build

Needs the .NET 8 SDK.

```powershell
dotnet build "src\Caelus\Caelus.csproj" -c Release
```

To build the installer (self-contained Setup.exe):

```powershell
.\publish.ps1
```

The installer is written to `dist\X Bootstrapper Setup.exe`.

## Usage

- First run of a portable build opens the installer.
- The Start Menu shortcut opens settings (`-menu`).
- `-player` launches Aisaka. A protocol URI from aisaka.me is passed through to the 2021 client.

## License

MIT. See [LICENSE](LICENSE).
