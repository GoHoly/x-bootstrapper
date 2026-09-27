X Bootstrapper 2.1.0 adds a tray icon, mod and FastFlag profiles, working Discord Rich Presence, and a safer updater, and fixes a long list of bugs.

## New
- **Tray icon** while a game is running: Open settings, Open logs, Exit (can be turned off in Behaviour)
- **Mods**: save, load and delete mod profiles; see which files are applied to the client; restore the original files or remove all mods. Originals are backed up before a mod replaces them and restored when you remove the mod or uninstall
- **FastFlags**: an editable name/value table with a filter, JSON import and export, and saved flag profiles
- **Discord Rich Presence** now actually connects (waits for Discord's READY reply). It needs your own Discord application ID (Integrations tab); with no ID nothing is sent. Activity tracking controls whether the place ID is shown and logged
- **Launch with the game**: start your own programs with Octane and optionally close them when the game exits
- **Updater**: progress window with Cancel, and the download is checked against GitHub's SHA-256 before it runs. Auto-update never restarts the app during a launch or a game session

## Fixed
- Large updates no longer time out after 100 seconds; stalled downloads are detected instead
- Closing a window no longer leaves an invisible X Bootstrapper process running; startup errors exit cleanly
- Choosing an install folder installs into `<folder>\X Bootstrapper`, and uninstall deletes only files X Bootstrapper created
- Website links are passed to the official Octane launcher per scheme (player or Studio), and X Bootstrapper waits for the new game process instead of grabbing one that was already open
- The official launcher's link handler is remembered and put back on uninstall or when you untick "Handle Play on octane.wtf"
- FastFlags you delete are removed from the client; Performance mode no longer forces other options on
- Settings are saved atomically with a backup and can't be overwritten by another X Bootstrapper window
- Deleted shortcuts are no longer recreated on every start
- Mod import never scans above the folder you picked; HUD atlas backups refresh when Octane updates them
- Installing an older version no longer gets undone by auto-update
- Opening an old Desktop copy no longer downgrades the installed app
- Log files are unique per process

## Changed
- Only `octane-player` and `octane-studio` links are registered. Old `caelus-*` and `roblox-*` handlers that point at X Bootstrapper are removed, so Roblox links are no longer taken over
- Removed the unused client-update option and the Deployment settings (the official Octane launcher keeps the client up to date)
- The uninstaller says what it does: the Octane client is never deleted, and files your mods replaced are restored
- The About page links to the Octane Discord

Existing installs update automatically from GitHub Releases. New users: download **X.Bootstrapper.Setup.exe**. No administrator account is required.
