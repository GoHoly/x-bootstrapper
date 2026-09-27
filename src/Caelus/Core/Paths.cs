namespace Caelus.Core;

public static class Paths
{
    public static string LocalAppData { get; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static string StartMenuRoot { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
    public static string StartMenu { get; private set; } = Path.Combine(StartMenuRoot, AppInfo.Name);
    public static string LegacyStartMenu { get; } = Path.Combine(StartMenuRoot, AppInfo.LegacyFolderName);
    public static string PreviousStartMenu { get; } = Path.Combine(StartMenuRoot, AppInfo.PreviousFolderName);
    public static string Desktop { get; } = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static string DefaultBase { get; } = Path.Combine(LocalAppData, AppInfo.Name);
    public static string LegacyBase { get; } = Path.Combine(LocalAppData, AppInfo.LegacyFolderName);
    public static string PreviousBase { get; } = Path.Combine(LocalAppData, AppInfo.PreviousFolderName);

    public static string Base { get; private set; } = DefaultBase;
    public static string Versions => Path.Combine(Base, "Versions");
    public static string Downloads => Path.Combine(Base, "Downloads");
    public static string Logs => Path.Combine(Base, "Logs");
    public static string Modifications => Path.Combine(Base, "Modifications");
    public static string ModBackups => Path.Combine(Base, "ModBackups");
    public static string ModProfiles => Path.Combine(Base, "ModProfiles");
    public static string PayloadManifest => Path.Combine(Base, ".xb-payload.txt");
    public static string Settings => Path.Combine(Base, "Settings.json");
    public static string State => Path.Combine(Base, "State.json");
    public static string Executable => Path.Combine(Base, AppInfo.ExeFileName);
    public static string LegacyExecutable => Path.Combine(Base, AppInfo.LegacyExeFileName);

    public static void Initialize(string? installDirectory = null)
    {
        Base = string.IsNullOrWhiteSpace(installDirectory) ? DefaultBase : installDirectory;
        StartMenu = Path.Combine(StartMenuRoot, AppInfo.Name);

        Directory.CreateDirectory(Base);
        Directory.CreateDirectory(Versions);
        Directory.CreateDirectory(Downloads);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Modifications);
    }

    public static bool IsLegacyDefault(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var full = Path.GetFullPath(path.TrimEnd('\\'));
        return string.Equals(full, Path.GetFullPath(LegacyBase), StringComparison.OrdinalIgnoreCase) ||
               string.Equals(full, Path.GetFullPath(PreviousBase), StringComparison.Ordinal);
    }
}
