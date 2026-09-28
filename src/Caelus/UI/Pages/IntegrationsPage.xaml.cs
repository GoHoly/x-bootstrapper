using System.Windows;
using System.Windows.Controls;
using Caelus.Core;
using Caelus.Models;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace Caelus.UI.Pages;

public partial class IntegrationsPage : System.Windows.Controls.UserControl
{
    private sealed record Row(Border Root, TextBox Name, TextBox Path, TextBox Arguments, CheckBox Enabled, CheckBox AutoClose);

    private readonly List<Row> _rows = new();
    private bool _ready;

    public IntegrationsPage()
    {
        InitializeComponent();
        var s = App.Settings.Prop;
        foreach (var choice in UiText.DiscordChoices)
            DiscordStatusBox.Items.Add(new ComboBoxItem { Content = choice });
        DiscordStatusBox.SelectedIndex = (int)s.EffectiveDiscordStatus;
        ShowDiscordHint();
        DiscordStatusBox.SelectionChanged += (_, _) =>
        {
            ShowDiscordHint();
            Write();
        };
        // The built-in ID is shown as an empty box ("use the default").
        DiscordIdBox.Text = s.DiscordClientId?.Trim() == AppInfo.DiscordClientId ? "" : s.DiscordClientId;
        ActivityBox.IsChecked = s.ActivityTracking;
        ActivityBox.Checked += (_, _) => Write();
        ActivityBox.Unchecked += (_, _) => Write();
        DiscordIdBox.LostFocus += (_, _) => Write();

        foreach (var item in s.Integrations ?? new List<Integration>())
            AddRow(item);
        UpdateEmpty();
        Unloaded += (_, _) => Write();
        _ready = true;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Choose a program to start with the game",
            Filter = "Programs|*.exe;*.bat;*.cmd;*.lnk|All files|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        AddRow(new Integration
        {
            Name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName),
            Path = dialog.FileName
        });
        UpdateEmpty();
        Write();
    }

    private void AddRow(Integration item)
    {
        var name = new TextBox { Text = item.Name };
        var path = new TextBox { Text = item.Path };
        var arguments = new TextBox { Text = item.Arguments };
        var enabled = new CheckBox { Content = "Enabled", IsChecked = item.Enabled };
        var autoClose = new CheckBox { Content = "Close with game", IsChecked = item.AutoClose, Margin = new Thickness(16, 0, 0, 0) };
        var browse = new Button { Content = "Browse", Style = (Style)FindResource("GhostButton"), Margin = new Thickness(8, 0, 0, 0) };
        var remove = new Button { Content = "Remove", Style = (Style)FindResource("GhostButton"), Margin = new Thickness(8, 0, 0, 0) };

        var panel = new StackPanel();
        panel.Children.Add(Label("Name"));
        panel.Children.Add(name);
        panel.Children.Add(Label("Program"));
        var pathRow = new DockPanel();
        DockPanel.SetDock(browse, Dock.Right);
        pathRow.Children.Add(browse);
        pathRow.Children.Add(path);
        panel.Children.Add(pathRow);
        panel.Children.Add(Label("Arguments (optional)"));
        panel.Children.Add(arguments);
        var options = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(remove, Dock.Right);
        options.Children.Add(remove);
        var checks = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        checks.Children.Add(enabled);
        checks.Children.Add(autoClose);
        options.Children.Add(checks);
        panel.Children.Add(options);

        var root = new Border
        {
            Child = panel,
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 10)
        };
        root.SetResourceReference(StyleProperty, "InsetPanel");

        var row = new Row(root, name, path, arguments, enabled, autoClose);
        _rows.Add(row);
        IntegrationRows.Children.Add(root);

        name.LostFocus += (_, _) => Write();
        path.LostFocus += (_, _) => Write();
        arguments.LostFocus += (_, _) => Write();
        enabled.Checked += (_, _) => Write();
        enabled.Unchecked += (_, _) => Write();
        autoClose.Checked += (_, _) => Write();
        autoClose.Unchecked += (_, _) => Write();
        browse.Click += (_, _) =>
        {
            using var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = "Choose a program",
                Filter = "Programs|*.exe;*.bat;*.cmd;*.lnk|All files|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                path.Text = dialog.FileName;
                Write();
            }
        };
        remove.Click += (_, _) =>
        {
            _rows.Remove(row);
            IntegrationRows.Children.Remove(root);
            UpdateEmpty();
            Write();
        };
    }

    private TextBlock Label(string text)
    {
        var label = new TextBlock { Text = text, Margin = new Thickness(0, 8, 0, 4), FontSize = 12 };
        label.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        return label;
    }

    private DiscordStatusMode SelectedStatus =>
        DiscordStatusBox.SelectedIndex is >= 0 and <= 2 ? (DiscordStatusMode)DiscordStatusBox.SelectedIndex : DiscordStatusMode.XBootstrapper;

    private void ShowDiscordHint()
    {
        DiscordStatusHint.Text = UiText.DiscordHint(SelectedStatus);
        // The application ID and place sharing only matter while X Bootstrapper sets the status.
        XbDiscordOptions.Visibility = SelectedStatus == DiscordStatusMode.XBootstrapper ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateEmpty() => EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void Write()
    {
        if (!_ready)
            return;

        var s = App.Settings.Prop;
        s.SetDiscordStatus(SelectedStatus);
        s.DiscordClientId = DiscordIdBox.Text.Trim();
        s.ActivityTracking = ActivityBox.IsChecked == true;
        s.Integrations = _rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Path.Text))
            .Select(row => new Integration
            {
                Name = row.Name.Text.Trim(),
                Path = row.Path.Text.Trim().Trim('"'),
                Arguments = row.Arguments.Text.Trim(),
                Enabled = row.Enabled.IsChecked == true,
                AutoClose = row.AutoClose.IsChecked == true
            })
            .ToList();
        App.Save();
    }
}
