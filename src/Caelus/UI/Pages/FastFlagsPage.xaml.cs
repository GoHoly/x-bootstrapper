using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Caelus.Core;
using Caelus.Models;
using Caelus.Services;

namespace Caelus.UI.Pages;

public partial class FastFlagsPage : System.Windows.Controls.UserControl
{
    private sealed record FlagRow(Grid Root, System.Windows.Controls.TextBox Name, System.Windows.Controls.TextBox Value);

    private readonly List<FlagRow> _flagRows = new();
    private bool _ready;

    public FastFlagsPage()
    {
        InitializeComponent();
        var s = App.Settings.Prop;
        s.FastFlags ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FpsBox.SelectedIndex = s.FramerateLimit switch
        {
            60 => 1,
            120 => 2,
            144 => 3,
            240 => 4,
            < 0 => 5,
            _ => 0
        };
        RenderBox.SelectedIndex = (int)s.RenderingMode;
        TextureBox.SelectedIndex = s.TextureQuality < 0 ? 0 : Math.Clamp(s.TextureQuality + 1, 0, 3);
        PostFxBox.IsChecked = s.DisablePostFx;
        FpsCounterBox.IsChecked = s.ShowFpsCounter;
        PerfBox.IsChecked = s.PerformanceMode;
        s.FastFlagProfiles ??= new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        LoadRows(s.FastFlags);
        RefreshProfiles();
        FilterBox.TextChanged += (_, _) => ApplyFilter();

        FpsBox.SelectionChanged += (_, _) => Write(presetsOnly: true);
        RenderBox.SelectionChanged += (_, _) => Write(presetsOnly: true);
        TextureBox.SelectionChanged += (_, _) => Write(presetsOnly: true);
        FpsBox.DropDownClosed += (_, _) => Write(presetsOnly: true);
        RenderBox.DropDownClosed += (_, _) => Write(presetsOnly: true);
        TextureBox.DropDownClosed += (_, _) => Write(presetsOnly: true);
        PostFxBox.Checked += (_, _) => Write(presetsOnly: true);
        PostFxBox.Unchecked += (_, _) => Write(presetsOnly: true);
        FpsCounterBox.Checked += (_, _) => Write(presetsOnly: true);
        FpsCounterBox.Unchecked += (_, _) => Write(presetsOnly: true);
        PerfBox.Checked += (_, _) =>
        {
            if (FpsBox.SelectedIndex == 0)
                FpsBox.SelectedIndex = 5;
            PostFxBox.IsChecked = true;
            FpsCounterBox.IsChecked = true;
            Write(presetsOnly: true);
        };
        PerfBox.Unchecked += (_, _) => Write(presetsOnly: true);
        Unloaded += (_, _) => Write(presetsOnly: false);
        _ready = true;
    }

    public void Flush() => Write(presetsOnly: false);

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Write(presetsOnly: false);
        FlagsStatus.Text = $"Saved {CollectFlags().Count} custom flag(s) into Octane's ClientSettings. Rejoin the game for them to take effect.";
    }

    // ---- table ----

    private void LoadRows(IEnumerable<KeyValuePair<string, string>> flags)
    {
        FlagRows.Children.Clear();
        _flagRows.Clear();
        foreach (var (name, value) in flags.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            AddRow(name, value);
        UpdateCount();
    }

    private FlagRow AddRow(string name, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });

        var nameBox = new System.Windows.Controls.TextBox { Text = name, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12 };
        var valueBox = new System.Windows.Controls.TextBox { Text = value, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12, Margin = new Thickness(8, 0, 0, 0) };
        var remove = new System.Windows.Controls.Button
        {
            Content = "✕",
            Style = (Style)FindResource("GhostButton"),
            Margin = new Thickness(8, 0, 0, 0),
            ToolTip = "Remove this flag"
        };
        Grid.SetColumn(valueBox, 1);
        Grid.SetColumn(remove, 2);
        grid.Children.Add(nameBox);
        grid.Children.Add(valueBox);
        grid.Children.Add(remove);

        var row = new FlagRow(grid, nameBox, valueBox);
        remove.Click += (_, _) =>
        {
            _flagRows.Remove(row);
            FlagRows.Children.Remove(grid);
            UpdateCount();
        };
        nameBox.TextChanged += (_, _) => UpdateCount();

        _flagRows.Add(row);
        FlagRows.Children.Add(grid);
        return row;
    }

    private void AddFlag_Click(object sender, RoutedEventArgs e)
    {
        FilterBox.Text = "";
        var row = AddRow("", "");
        row.Name.Focus();
        UpdateCount();
    }

    private void ApplyFilter()
    {
        var filter = FilterBox.Text.Trim();
        foreach (var row in _flagRows)
        {
            row.Root.Visibility = filter.Length == 0 || row.Name.Text.Contains(filter, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void UpdateCount()
    {
        var names = _flagRows.Select(row => row.Name.Text.Trim()).Where(name => name.Length > 0).ToList();
        var duplicates = names.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        var invalid = names.Where(name => name.Any(char.IsWhiteSpace)).ToList();
        FlagsStatus.Text = _flagRows.Count == 0
            ? "No custom flags. Add one, or import a JSON file."
            : duplicates.Count > 0
                ? $"Duplicate name(s): {string.Join(", ", duplicates)}. The last one wins."
                : invalid.Count > 0
                    ? $"Flag names can't contain spaces: {string.Join(", ", invalid)}"
                    : $"{names.Count} custom flag(s). Press Save flags to write them.";
    }

    private Dictionary<string, string> CollectFlags()
    {
        var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _flagRows)
        {
            var name = row.Name.Text.Trim();
            if (name.Length == 0 || name.Any(char.IsWhiteSpace))
                continue;
            flags[name] = row.Value.Text.Trim();
        }

        return flags;
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Import FastFlags JSON",
            Filter = "JSON files|*.json|All files|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        try
        {
            var imported = ParseFlagsJson(File.ReadAllText(dialog.FileName));
            var merged = CollectFlags();
            foreach (var (name, value) in imported)
                merged[name] = value;
            LoadRows(merged);
            FlagsStatus.Text = $"Imported {imported.Count} flag(s) from {Path.GetFileName(dialog.FileName)}. Press Save flags to write them.";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"That file isn't a FastFlags JSON object.\n\n{ex.Message}", AppInfo.Name,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    internal static Dictionary<string, string> ParseFlagsJson(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new FormatException("Expected an object like { \"FlagName\": \"value\" }.");

        var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            flags[property.Name.Trim()] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? "",
                JsonValueKind.True => "True",
                JsonValueKind.False => "False",
                JsonValueKind.Number => property.Value.GetRawText(),
                _ => throw new FormatException($"{property.Name} has a {property.Value.ValueKind} value; only text, numbers, and true/false are supported.")
            };
        }

        return flags;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.SaveFileDialog
        {
            Title = "Export FastFlags JSON",
            Filter = "JSON files|*.json",
            FileName = "fastflags.json"
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        try
        {
            var flags = CollectFlags().OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(flags, new JsonSerializerOptions { WriteIndented = true }));
            FlagsStatus.Text = $"Exported {flags.Count} flag(s) to {Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Could not export.\n\n{ex.Message}", AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ---- profiles ----

    private void RefreshProfiles(string? select = null)
    {
        var names = App.Settings.Prop.FastFlagProfiles.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        ProfileBox.ItemsSource = names;
        if (select is not null && names.Contains(select))
            ProfileBox.SelectedItem = select;
        else if (names.Count > 0)
            ProfileBox.SelectedIndex = 0;
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var name = (string.IsNullOrWhiteSpace(ProfileNameBox.Text) ? ProfileBox.SelectedItem as string : ProfileNameBox.Text)?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            FlagsStatus.Text = "Type a profile name first.";
            return;
        }

        var profiles = App.Settings.Prop.FastFlagProfiles;
        var existing = profiles.Keys.FirstOrDefault(key => key.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            profiles.Remove(existing);
        profiles[name] = CollectFlags();
        App.Save();
        ProfileNameBox.Text = "";
        RefreshProfiles(name);
        FlagsStatus.Text = $"Saved {profiles[name].Count} flag(s) as \"{name}\".";
    }

    private void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not string name ||
            !App.Settings.Prop.FastFlagProfiles.TryGetValue(name, out var flags))
            return;

        LoadRows(flags ?? new Dictionary<string, string>());
        Write(presetsOnly: false);
        FlagsStatus.Text = $"Loaded \"{name}\" ({flags?.Count ?? 0} flag(s)) and wrote it to Octane.";
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not string name)
            return;
        if (System.Windows.MessageBox.Show($"Delete the flag profile \"{name}\"?", AppInfo.Name,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        App.Settings.Prop.FastFlagProfiles.Remove(name);
        App.Save();
        RefreshProfiles();
    }

    private void Write(bool presetsOnly)
    {
        if (!_ready)
            return;

        var s = App.Settings.Prop;
        s.FramerateLimit = FpsBox.SelectedIndex switch
        {
            1 => 60,
            2 => 120,
            3 => 144,
            4 => 240,
            5 => -1,
            _ => 0
        };
        s.RenderingMode = (RenderingMode)Math.Clamp(RenderBox.SelectedIndex, 0, 3);
        s.TextureQuality = TextureBox.SelectedIndex <= 0 ? -1 : TextureBox.SelectedIndex - 1;
        s.DisablePostFx = PostFxBox.IsChecked == true;
        s.ShowFpsCounter = FpsCounterBox.IsChecked == true;
        s.PerformanceMode = PerfBox.IsChecked == true;

        if (!presetsOnly)
        {
            s.FastFlags = CollectFlags();
        }

        App.Save();
        if (UiShots.Active)
            return;
        try
        {
            FastFlagService.ApplyAll(s, App.State.Prop, ClientLocator.Find(s, App.State.Prop), log: true);
        }
        catch (Exception ex)
        {
            Logger.Error("FastFlags", ex);
        }
    }
}
