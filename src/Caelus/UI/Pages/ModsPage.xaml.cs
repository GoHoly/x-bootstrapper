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
        RefreshProfiles();
        Loaded += (_, _) => RefreshApplied();
    }

    private void RefreshProfiles(string? select = null)
    {
        var profiles = ModProfileService.List();
        ProfileBox.ItemsSource = profiles;
        if (select is not null && profiles.Contains(select))
            ProfileBox.SelectedItem = select;
        else if (profiles.Count > 0 && ProfileBox.SelectedIndex < 0)
            ProfileBox.SelectedIndex = 0;
    }

    private void RefreshApplied()
    {
        try
        {
            var install = ClientLocator.Find(App.Settings.Prop, App.State.Prop);
            if (install is null)
            {
                AppliedSummary.Text = "No Octane client found yet.";
                AppliedList.Text = "";
                return;
            }

            var files = ModService.AppliedFiles(install);
            AppliedSummary.Text = files.Count == 0
                ? $"No mod files are applied to {install.VersionDirectory}."
                : $"{files.Count} file(s) replaced in {install.VersionDirectory}. The originals are backed up.";
            AppliedList.Text = string.Join(Environment.NewLine, files);
        }
        catch (Exception ex)
        {
            AppliedSummary.Text = $"Could not read the applied files: {ex.Message}";
        }
    }

    private void RefreshApplied_Click(object sender, RoutedEventArgs e) => RefreshApplied();

    private void RestoreOriginals_Click(object sender, RoutedEventArgs e)
    {
        Run("Could not restore the original files.", () =>
        {
            var restored = ModService.RestoreAllOriginals();
            RefreshStatus($"Restored {restored} original file(s). Your mods are applied again at the next launch.");
        });
    }

    private void RemoveAll_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show(
                "Remove every mod and put the client's original files back?\n\nYour current mods are saved as the profile \"Before last load\" first.",
                AppInfo.Name, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        Run("Could not remove the mods.", () =>
        {
            ModProfileService.RemoveAllMods();
            RefreshProfiles(ModProfileService.AutoSaveName);
            RefreshStatus("All mods removed and the original files restored.");
        });
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(ProfileNameBox.Text) ? ProfileBox.SelectedItem as string : ProfileNameBox.Text;
        Run("Could not save the profile.", () =>
        {
            var saved = ModProfileService.Save(name ?? "");
            ProfileNameBox.Text = "";
            RefreshProfiles(saved);
            RefreshStatus($"Saved your current mods as \"{saved}\".");
        });
    }

    private void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not string name)
            return;

        Run("Could not load the profile.", () =>
        {
            ModProfileService.Load(name);
            RefreshProfiles(name);
            RefreshStatus($"Loaded \"{name}\" and applied it to Octane. Fully close the game, then Play again.");
        });
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not string name)
            return;
        if (System.Windows.MessageBox.Show($"Delete the profile \"{name}\"?", AppInfo.Name,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        Run("Could not delete the profile.", () =>
        {
            ModProfileService.Delete(name);
            ProfileBox.SelectedIndex = -1;
            RefreshProfiles();
        });
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
            RefreshStatus();
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
            if (success is not null)
                RefreshStatus(success);
            RefreshApplied();
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
