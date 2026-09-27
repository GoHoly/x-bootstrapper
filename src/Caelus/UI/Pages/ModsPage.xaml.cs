using System.Windows;
using System.Windows.Forms;
using Caelus.Core;
using Caelus.Services;

namespace Caelus.UI.Pages;

public partial class ModsPage : System.Windows.Controls.UserControl
{
    public ModsPage()
    {
        InitializeComponent();
        ModService.MigrateLegacyCursor();
        RebuildSlots();
        RefreshStatus();
    }

    private void RebuildSlots()
    {
        SlotsPanel.Children.Clear();
        string? lastGroup = null;
        foreach (var slot in ModService.Slots)
        {
            if (slot.Group != lastGroup)
            {
                lastGroup = slot.Group;
                var header = new System.Windows.Controls.TextBlock
                {
                    Text = slot.Group.ToUpperInvariant(),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    Margin = new Thickness(0, SlotsPanel.Children.Count == 0 ? 0 : 14, 0, 8)
                };
                header.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "MutedBrush");
                SlotsPanel.Children.Add(header);
            }

            SlotsPanel.Children.Add(CreateRow(slot));
        }
    }

    private UIElement CreateRow(ModSlot slot)
    {
        var installed = ModService.HasSlot(slot);
        var fileName = installed ? Path.GetFileName(ModService.SlotPath(slot)) : null;

        var choose = new System.Windows.Controls.Button
        {
            Content = installed ? "Replace" : "Choose file",
            Style = (Style)FindResource("AccentButton"),
            Tag = slot
        };
        choose.Click += ChooseSlot_Click;

        var remove = new System.Windows.Controls.Button
        {
            Content = "Remove",
            Style = (Style)FindResource("GhostButton"),
            Margin = new Thickness(8, 0, 0, 0),
            IsEnabled = installed,
            Tag = slot
        };
        remove.Click += RemoveSlot_Click;

        var buttons = new System.Windows.Controls.WrapPanel
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        buttons.Children.Add(choose);
        buttons.Children.Add(remove);

        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

        var labels = new System.Windows.Controls.StackPanel();
        labels.Children.Add(new System.Windows.Controls.TextBlock { Text = slot.Title, FontWeight = FontWeights.SemiBold });
        var hint = new System.Windows.Controls.TextBlock
        {
            Text = installed ? $"Using {fileName}" : slot.Hint,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0)
        };
        hint.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "MutedBrush");
        labels.Children.Add(hint);

        System.Windows.Controls.Grid.SetColumn(labels, 0);
        System.Windows.Controls.Grid.SetColumn(buttons, 1);
        grid.Children.Add(labels);
        grid.Children.Add(buttons);
        return grid;
    }

    private void ChooseSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: ModSlot slot })
            return;

        using var dialog = new OpenFileDialog
        {
            Title = $"Choose {slot.Title.ToLowerInvariant()}",
            Filter = slot.Filter,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        Run("Could not add that file.", () =>
        {
            Directory.CreateDirectory(Paths.Modifications);
            ModService.SetSlot(slot, dialog.FileName);
        });
    }

    private void RemoveSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: ModSlot slot })
            return;

        Run("Could not remove that mod.", () =>
        {
            ModService.ClearSlot(slot);
            RebuildSlots();
            RefreshStatus();
        });
    }

    private void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a mod folder",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        Run("Could not import that folder.", () =>
        {
            var count = ModService.ImportFolder(dialog.SelectedPath);
            RefreshStatus($"Imported {count} file(s) and applied them to Octane. Fully close the game, then Play again.");
        });
    }

    private void ImportZip_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Import a mod zip",
            Filter = "Zip files|*.zip|All files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        Run("Could not import that zip.", () =>
        {
            var count = ModService.ImportZip(dialog.FileName);
            RefreshStatus($"Imported {count} file(s) and applied them to Octane. Fully close the game, then Play again.");
        });
    }

    private void Open_Click(object sender, RoutedEventArgs e) => ModService.OpenFolder();

    private void Run(string errorTitle, Action action, string? success = null)
    {
        try
        {
            action();
            RebuildSlots();
            RefreshStatus(success);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"{errorTitle}\n\n{ex.Message}", AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshStatus(string? extra = null)
    {
        var count = ModService.Count();
        StatusText.Text = extra ?? (count == 0
            ? "No mods yet. Choose a file above, then press Play on octane.wtf."
            : $"{count} mod file(s) ready. They apply the next time Octane starts.");
    }
}
