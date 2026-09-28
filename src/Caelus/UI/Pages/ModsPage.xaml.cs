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
        Loaded += async (_, _) => await RebuildSkyAsync();
    }

    // ------------------------------------------------------------------ sky

    private static readonly Dictionary<string, string> FaceNames = new()
    {
        ["bk"] = "back (bk)",
        ["dn"] = "bottom (dn)",
        ["ft"] = "front (ft)",
        ["lf"] = "left (lf)",
        ["rt"] = "right (rt)",
        ["up"] = "top (up)"
    };

    private bool _skyBusy;

    /// <summary>Builds the sky tiles (Default, the built-ins, Custom) with previews.</summary>
    internal async Task RebuildSkyAsync()
    {
        var current = SkyboxService.CurrentId();
        var items = new List<(string Id, string Name, string Blurb)> { (SkyboxService.DefaultId, "Default", "Octane's own sky, restored exactly.") };
        items.AddRange(SkyboxService.BuiltIn.Select(p => (p.Id, p.Name, p.Blurb)));
        items.Add((SkyboxService.CustomId, "Custom", current == SkyboxService.CustomId ? "Your six images." : "Pick six images (any size)."));

        var previews = await Task.Run(() => items.ToDictionary(item => item.Id, item =>
        {
            try
            {
                return item.Id == SkyboxService.CustomId && current != SkyboxService.CustomId ? null : SkyboxService.Preview(item.Id, 42);
            }
            catch (Exception ex)
            {
                Logger.Write("Sky", $"No preview for {item.Id}: {ex.Message}");
                return null;
            }
        }));

        SkyPanel.Children.Clear();
        foreach (var item in items)
            SkyPanel.Children.Add(SkyTile(item.Id, item.Name, item.Blurb, previews[item.Id], item.Id == current));
        if (!_skyBusy)
            SkyStatus.Text = $"Current sky: {SkyboxService.CurrentName()}.";
    }

    private UIElement SkyTile(string id, string name, string blurb, System.Windows.Media.ImageSource? preview, bool selected)
    {
        var modern = ThemeService.IsModern;
        var tile = new System.Windows.Controls.Border
        {
            Width = 168,
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(modern ? 10 : 4),
            BorderThickness = new Thickness(selected ? 2 : 1),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = id
        };
        tile.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "InputBrush");
        tile.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, selected ? "AccentBrush" : "BorderBrush");

        var art = new System.Windows.Controls.Border
        {
            Height = 42,
            CornerRadius = new CornerRadius(modern ? 6 : 2),
            ClipToBounds = true
        };
        art.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "CardBrush");
        if (preview is not null)
        {
            art.Background = new System.Windows.Media.ImageBrush(preview) { Stretch = System.Windows.Media.Stretch.Fill };
        }
        else
        {
            var plus = new System.Windows.Controls.TextBlock
            {
                Text = id == SkyboxService.CustomId ? "+ 6 images" : "no preview",
                FontSize = 11,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            plus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "MutedBrush");
            art.Child = plus;
        }

        var title = new System.Windows.Controls.TextBlock
        {
            Text = selected ? name + "  ✓" : name,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(2, 7, 2, 0)
        };
        var text = new System.Windows.Controls.TextBlock
        {
            Text = blurb,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 2, 2, 2)
        };
        text.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "MutedBrush");

        var stack = new System.Windows.Controls.StackPanel();
        stack.Children.Add(art);
        stack.Children.Add(title);
        stack.Children.Add(text);
        tile.Child = stack;
        tile.MouseLeftButtonUp += async (_, _) =>
        {
            if (id == SkyboxService.CustomId)
                await PickCustomSkyAsync();
            else
                await ApplySkyAsync(id);
        };
        return tile;
    }

    /// <summary>What a Default / built-in tile click does.</summary>
    internal async Task<bool> ApplySkyAsync(string id)
    {
        return await RunSkyAsync(id == SkyboxService.DefaultId ? "Restoring the default sky…" : "Making the sky…", () =>
        {
            if (id == SkyboxService.DefaultId)
                SkyboxService.RestoreDefault();
            else
                SkyboxService.ApplyBuiltIn(id);
        });
    }

    private async Task PickCustomSkyAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose six sky images (names ending in _bk, _dn, _ft, _lf, _rt, _up), or cancel to pick them one by one",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*",
            Multiselect = true,
            CheckFileExists = true
        };
        var picked = dialog.ShowDialog() == DialogResult.OK ? dialog.FileNames : Array.Empty<string>();
        var faces = SkyboxService.MatchFaces(picked, out _);

        // Anything the names didn't place is asked for one face at a time.
        foreach (var face in SkyboxService.Faces.Where(face => !faces.ContainsKey(face)))
        {
            using var single = new OpenFileDialog
            {
                Title = $"Choose the {FaceNames[face]} sky image",
                Filter = dialog.Filter,
                CheckFileExists = true
            };
            if (single.ShowDialog() != DialogResult.OK)
            {
                SkyStatus.Text = "Custom sky cancelled; nothing changed.";
                return;
            }

            faces[face] = single.FileName;
        }

        await ApplyCustomSkyAsync(faces);
    }

    /// <summary>Converts and applies six picked images (face -> file).</summary>
    internal async Task<bool> ApplyCustomSkyAsync(IReadOnlyDictionary<string, string> images)
    {
        Dictionary<string, byte[]> loaded;
        try
        {
            loaded = SkyboxService.LoadCustom(images);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Could not read those images.\n\n{ex.Message}", AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        var description = string.Join(", ", SkyboxService.Faces.Select(face => Path.GetFileName(images[face])));
        return await RunSkyAsync("Converting your images…", () => SkyboxService.ApplyCustom(loaded, description));
    }

    private async Task<bool> RunSkyAsync(string busyText, Action work)
    {
        if (_skyBusy)
            return false;

        _skyBusy = true;
        SkyPanel.IsEnabled = false;
        SkyStatus.Text = busyText;
        try
        {
            await Task.Run(work);
            var running = ClientLocator.RunningIds(ClientLocator.PlayerProcessNames).Count > 0;
            SkyStatus.Text = $"Sky set to {SkyboxService.CurrentName()}." +
                             (running ? " Octane is running: restart it to see the change." : " It shows the next time Octane starts.");
            RefreshStatus();
            RefreshApplied();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Sky", ex);
            SkyStatus.Text = $"Could not change the sky: {ex.Message}";
            return false;
        }
        finally
        {
            _skyBusy = false;
            SkyPanel.IsEnabled = true;
            await RebuildSkyAsync();
        }
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
            // Modern keeps the accent for page-level actions; a column of accent buttons is noise.
            Style = (Style)FindResource(ThemeService.IsModern ? "GhostButton" : "AccentButton"),
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
