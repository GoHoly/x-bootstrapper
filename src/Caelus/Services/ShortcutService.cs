using Caelus.Core;

namespace Caelus.Services;

public static class ShortcutService
{
    private static string BrandShortcut => AppInfo.Name + ".lnk";
    private const string LegacyShortcut = "Caelus.lnk";

    public static void CreateAll() => Publish();

    public static void Publish()
    {
        Directory.CreateDirectory(Paths.StartMenu);
        Create(Path.Combine(Paths.StartMenu, BrandShortcut), Paths.Executable, "-menu", AppInfo.Name);
        Create(Path.Combine(Paths.StartMenu, "Aisaka.lnk"), Paths.Executable, "-player", "Launch Aisaka");
        Create(Path.Combine(Paths.Desktop, BrandShortcut), Paths.Executable, "-menu", AppInfo.Name);

        foreach (var leftover in new[]
        {
            Path.Combine(Paths.StartMenu, LegacyShortcut),
            Path.Combine(Paths.Desktop, LegacyShortcut),
            Path.Combine(Paths.LegacyStartMenu, LegacyShortcut),
            Path.Combine(Paths.LegacyStartMenu, "Aisaka.lnk")
        })
            TryDelete(leftover);

        TryDeleteDirectory(Paths.LegacyStartMenu);
        TryDeleteDirectory(Paths.PreviousStartMenu);
    }

    public static void MigrateLegacy() => Publish();

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
