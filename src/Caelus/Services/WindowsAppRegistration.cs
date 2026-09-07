using Microsoft.Win32;
using Caelus.Core;

namespace Caelus.Services;

public static class WindowsAppRegistration
{
    public static void Register()
    {
        var exe = File.Exists(Paths.Executable) ? Paths.Executable : Environment.ProcessPath!;

        if (!File.Exists(Path.Combine(Paths.Base, "unins000.exe")))
        {
            var uninstall = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppInfo.Name}";
            using (var key = Registry.CurrentUser.CreateSubKey(uninstall))
            {
                if (key is not null)
                {
                    key.SetValue("DisplayName", AppInfo.Name);
                    key.SetValue("DisplayIcon", exe);
                    key.SetValue("Publisher", AppInfo.Name);
                    key.SetValue("InstallLocation", Paths.Base);
                    key.SetValue("UninstallString", $"\"{exe}\" -uninstall");
                    key.SetValue("DisplayVersion", AppInfo.Version);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
        }

        using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{AppInfo.ExeFileName}"))
        {
            if (key is not null)
            {
                key.SetValue("", exe);
                key.SetValue("Path", Paths.Base);
            }
        }

        TryDeleteKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Caelus");
        TryDeleteKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\App Paths\Caelus.exe");
        TryDeleteKey(Registry.CurrentUser, @"Software\Classes\Applications\Caelus.exe");
        WriteFolderIcon(exe);
    }

    public static void Unregister()
    {
        TryDeleteKey(Registry.CurrentUser, $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppInfo.Name}");
        TryDeleteKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Caelus");
        TryDeleteKey(Registry.CurrentUser, $@"Software\Microsoft\Windows\CurrentVersion\App Paths\{AppInfo.ExeFileName}");
        TryDeleteKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\App Paths\Caelus.exe");
        TryDeleteKey(Registry.CurrentUser, @"Software\Classes\Applications\Caelus.exe");
    }

    private static void WriteFolderIcon(string exe)
    {
        try
        {
            var ini = Path.Combine(Paths.Base, "desktop.ini");
            File.WriteAllText(ini, $"[.ShellClassInfo]\r\nIconResource={exe},0\r\nInfoTip={AppInfo.Name}\r\n");
            File.SetAttributes(ini, FileAttributes.Hidden | FileAttributes.System);
            File.SetAttributes(Paths.Base, File.GetAttributes(Paths.Base) | FileAttributes.System);
        }
        catch (Exception ex)
        {
            Logger.Error("WindowsApp", ex);
        }
    }

    private static void TryDeleteKey(RegistryKey root, string path)
    {
        try
        {
            root.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            Logger.Error("WindowsApp", ex);
        }
    }
}
