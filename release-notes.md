X Bootstrapper 2.3.3 adds Windows App Beta launch support for Octane.

## New
- **Windows App Beta (Octane App).** Behaviour has **Launch the Windows App Beta home (Octane App)**. When it is on, **Launch Octane** starts the client with `--app` (the same path as the old Roblox / Aisaka App Beta), instead of a blank player. Website Play links still join games as before. You can also use `X Bootstrapper.exe -app`, or the Start Menu **Octane App** shortcut after installing
- **launchmode:app links.** If a protocol URI asks for App Beta (`launchmode:app`), X Bootstrapper launches the player with `--app` itself. It does not hand those links to OctanePlayerLauncher (which only understands joins), which is what turned the DevForum method into a white screen on Octane

## Important
- **Octane's current client often cannot open App Beta.** Stock `OctanePlayer.exe` no longer includes the `--app` launch path (and OctanePlayerLauncher has no App Beta handling). When that is the case, X Bootstrapper shows a clear message instead of a white window. If Octane restores App Beta, or you point Install → client folder at a 2021 build that still has it, the same setting works without another update

## Also in 2.3.2
Theme picking always applies, and themes get their own neon logos (Octane purple, Classic blue, Paper/Dusk red, xyxy pink; Midnight keeps the original X).

## Also in 2.3
Sky picker, Discord status choice, website-link health warning, Copy logs, first-run setup, What's new, and exit-on-close by default. See earlier notes for detail.

Existing installs update automatically from GitHub Releases. New users: download **X.Bootstrapper.Setup.exe**. No administrator account is required.
