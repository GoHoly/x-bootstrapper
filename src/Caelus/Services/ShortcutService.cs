using Caelus.Core;

namespace Caelus.Services;

public static class ShortcutService
{
    private static string BrandShortcut => AppInfo.Name + ".lnk";
    private const string LegacyShortcut = "Caelus.lnk";

    /// <summary>
    /// Creates the Start Menu and desktop shortcuts when <paramref name="createMissing"/> is true
    /// (first install). On normal starts only shortcuts that already exist are refreshed, so a
    /// shortcut you deleted or never wanted is not recreated.
    /// </summary>
    public static void Publish(bool createMissing = false)
    {
        if (createMissing)
            Directory.CreateDirectory(Paths.StartMenu);

        Refresh(Path.Combine(Paths.StartMenu, BrandShortcut), "-menu", AppInfo.Name, createMissing);
        Refresh(Path.Combine(Paths.StartMenu, "Octane.lnk"), "-player", "Launch Octane", createMissing);
        Refresh(Path.Combine(Paths.Desktop, BrandShortcut), "-menu", AppInfo.Name, createMissing);

        foreach (var leftover in new[]
        {
            Path.Combine(Paths.StartMenu, LegacyShortcut),
            Path.Combine(Paths.Desktop, LegacyShortcut),
            Path.Combine(Paths.LegacyStartMenu, LegacyShortcut),
            Path.Combine(Paths.LegacyStartMenu, "Octane.lnk")
        })
            TryDelete(leftover);

        TryDeleteDirectory(Paths.LegacyStartMenu);
        TryDeleteDirectory(Paths.PreviousStartMenu);
    }

    private static void Refresh(string path, string arguments, string description, bool createMissing)
    {
        if (createMissing || File.Exists(path))
            Create(path, Paths.Executable, arguments, description);
    }

    public static void RemoveAll()
    {
        TryDelete(Path.Combine(Paths.Desktop, BrandShortcut));
        TryDelete(Path.Combine(Paths.Desktop, LegacyShortcut));
        TryDeleteDirectory(Paths.StartMenu);
        TryDeleteDirectory(Paths.LegacyStartMenu);
        TryDeleteDirectory(Paths.PreviousStartMenu);
    }

    private static void Create(string path, string target, string arguments, string description)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null)
                return;

            var shell = Activator.CreateInstance(type);
            if (shell is null)
                return;

            var shortcut = type.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { path });
            if (shortcut is null)
                return;

            var shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { target });
            shortcutType.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
            shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(target)! });
            shortcutType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { description });
            shortcutType.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { $"{target},0" });
            shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);

            try
            {
                ShellLinkHelper.StampAppUserModelId(path, AppInfo.AppUserModelId);
            }
            catch (Exception ex)
            {
                Logger.Error("Shortcuts", ex);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Shortcuts", ex);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Logger.Error("Shortcuts", ex);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            Logger.Error("Shortcuts", ex);
        }
    }
}
