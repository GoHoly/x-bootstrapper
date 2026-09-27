X Bootstrapper 2.2.1 fixes the Classic style toggle and Discord Rich Presence, and gives the Octane theme Octane's own colors.

## Fixed
- **Classic style toggle works every time.** Switching styles rebuilds the Appearance page, and before this fix the page jumped back to the top and the Classic check box only reacted on its small box and label. So once Classic was on, clicking the toggle again usually hit empty space and nothing happened. The toggle now stays exactly where it was (under the pointer, with keyboard focus), and the whole row is clickable in both styles, so you can switch back and forth as often as you like. The choice is saved and kept after a restart
- **Discord Rich Presence shows up while you play.** Discord only displays one Rich Presence from your PC at a time and keeps whichever app connected first. The Octane client connects its own a moment after it starts, so X Bootstrapper's presence was accepted but never shown. X Bootstrapper now connects as soon as a launch begins, so its presence is the one Discord shows (Discord can't show two at once), keeps it for the whole game session, and clears it when the game closes. The presence image also shows now (the old image key didn't exist on Discord's side). Discord replies are now written to the log
- **Play on octane.wtf keeps going through X Bootstrapper.** Octane's own launcher, which X Bootstrapper hands the join link to, took the octane-player:// and octane-studio:// links back each time it ran, so the next Play skipped X Bootstrapper entirely (no presence, FastFlags or mods). X Bootstrapper now takes the links back once the game is running and again when it closes

## Changed
- **Octane theme in Octane's colors:** near-black, purple-tinted backgrounds with a vivid purple accent, clearly different from Midnight's soft violet. The menu, launch, update and installer windows all use it. The Classic style's Octane colors are unchanged

Existing installs update automatically from GitHub Releases. New users: download **X.Bootstrapper.Setup.exe**. No administrator account is required.
