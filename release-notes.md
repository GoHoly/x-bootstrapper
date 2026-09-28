X Bootstrapper 2.3.0 adds a sky picker, a Discord status choice, a warning when Play on octane.wtf stops going through X Bootstrapper, one-click log bundles, a first-run setup and a What's new window. Closing X Bootstrapper now exits it completely by default.

## New
- **Sky picker (Mods page).** Pick Sunset, Starry night, Purple nebula or Clear day, or use your own six images of any size (names ending in _bk, _dn, _ft, _lf, _rt, _up are placed automatically, anything else is asked for face by face). X Bootstrapper converts them to the client's own sky format. **Default** puts Octane's original sky back exactly. Games that set their own sky keep theirs
- **Choose whose Discord status shows.** Integrations now has a Discord status setting: **X Bootstrapper status** (as before), **Octane's own status**, or **None**. Discord shows one game status from your PC at a time, so with Octane's own or None, X Bootstrapper doesn't connect to Discord at all. X Bootstrapper can't switch off the status the Octane client sets itself; to hide that too, use Discord's Activity Privacy setting. Your old Rich Presence setting carries over
- **Warning when the website links are taken.** Octane's launcher sometimes takes the octane-player:// and octane-studio:// links back, and then Play on octane.wtf skips your mods, FastFlags and Discord status choice. The menu now checks this while it's open and shows a banner with a **Fix links** button, and the log records who took them
- **Copy logs (About page).** Puts the newest logs, your version and a short settings summary into one zip on your Desktop, ready for a bug report. Anything that looks like a token, ticket, cookie, key or join link is removed first, and your user folder path is replaced with %USERPROFILE%
- **First-run setup.** New installs get a short setup: theme and style, Discord status, a website links check and the background setting. Skip it if you like; About can open it again
- **What's new.** After an update, these notes open once, over the menu. They're built into the app, so they work offline. You can turn this off, and About can open them any time

## Changed
- **Closing X Bootstrapper exits it completely.** A new setting in Behaviour (and in the first-run setup), **Keep running in the background after closing**, is **off** by default. With it off, closing the window leaves nothing running in Task Manager. When you start a game through X Bootstrapper, a small helper stays only as long as it's needed: until the game closes if X Bootstrapper shows your Discord status or closes programs with the game, otherwise about 15 seconds, to take the website links back. Turn the setting on to keep X Bootstrapper in the notification area after closing it. From there it keeps the website links pointed at X Bootstrapper, and you can exit it from the tray icon

## Fixed
- **X Bootstrapper's Discord status keeps its head start.** 2.2.1 connected to Discord as the launch began, but dropped that connection and reconnected once the game had started, so Octane's own status could still get in first. The connection opened at launch is now kept for the whole game session

## Not included
- **Replacing in-game assets (decals, meshes, sounds by asset ID).** Octane downloads these into a shared, checksummed web cache that it checks again every day, so swapping them would mean editing that cache (undone silently) or intercepting the game's traffic. It could also make some things easier to see in competitive games. Local content (sky, sounds, cursors, fonts, UI) is still covered by Mods

Existing installs update automatically from GitHub Releases. New users: download **X.Bootstrapper.Setup.exe**. No administrator account is required.
