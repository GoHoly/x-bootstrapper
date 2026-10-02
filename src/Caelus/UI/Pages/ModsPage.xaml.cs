using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
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
        RebuildBuiltInPresets();
        Loaded += (_, _) => RefreshApplied();
        Loaded += async (_, _) => await RebuildSkyAsync();
        Unloaded += (_, _) => ModAudioPreview.Stop();
    }

    // ------------------------------------------------------------------ sky

    private bool _skyBusy;

    /// <summary>Builds the sky tiles (Default, the built-ins, Custom) with previews.</summary>
    internal async Task RebuildSkyAsync()
    {
        var current = SkyboxService.CurrentId();
        var items = new List<(string Id, string Name, string Blurb)> { (SkyboxService.DefaultId, "Default", "Octane's own sky, restored exactly.") };
        items.AddRange(SkyboxService.BuiltIn.Select(p => (p.Id, p.Name, p.Blurb)));
        items.Add((SkyboxService.CustomId, "Custom", current == SkyboxService.CustomId ? "Your six images â€” click to edit." : "Open the custom sky editor with a 3D preview."));

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
                Text = id == SkyboxService.CustomId ? "+ Preview" : "no preview",
                FontSize = 11,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            plus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "MutedBrush");
            art.Child = plus;
        }

        var title = new System.Windows.Controls.TextBlock
        {
            Text = selected ? name + "  âœ“" : name,
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
        ThemeService.OnPress(tile, () =>
        {
            if (id == SkyboxService.CustomId)
                _ = OpenCustomSkyAsync();
            else
                _ = ApplySkyAsync(id);
        });
        return tile;
    }

    /// <summary>What a Default / built-in tile click does.</summary>
    internal async Task<bool> ApplySkyAsync(string id)
    {
        return await RunSkyAsync(id == SkyboxService.DefaultId ? "Restoring the default skyâ€¦" : "Making the skyâ€¦", () =>
        {
            if (id == SkyboxService.DefaultId)
                SkyboxService.RestoreDefault();
            else
                SkyboxService.ApplyBuiltIn(id);
        });
    }

    private async Task OpenCustomSkyAsync()
    {
        if (_skyBusy)
            return;

        try
        {
            var dialog = new CustomSkyWindow
            {
                Owner = Window.GetWindow(this)
            };
            var result = dialog.ShowDialog();
            if (result != true || !dialog.Applied)
            {
                if (!_skyBusy)
                    SkyStatus.Text = $"Current sky: {SkyboxService.CurrentName()}.";
                return;
            }

            var running = ClientLocator.RunningIds(ClientLocator.PlayerProcessNames).Count > 0;
            SkyStatus.Text = $"Sky set to {SkyboxService.CurrentName()}." +
                             (running ? " Octane is running: restart it to see the change." : " It shows the next time Octane starts.");
            RefreshStatus();
            RefreshApplied();
            await RebuildSkyAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("Sky", ex);
            SkyStatus.Text = $"Could not open the custom sky editor: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"Could not open the custom sky editor.\n\n{ex.Message}",
                AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Converts and applies six picked images (face -> file). Used by tests and the custom sky window.</summary>
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
        return await RunSkyAsync("Converting your imagesâ€¦", () => SkyboxService.ApplyCustom(loaded, description));
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
        var profiles = ModProfileService.ListUserPresets();
        ProfileBox.ItemsSource = profiles.Select(p => p.Name).ToList();
        if (select is not null && profiles.Any(p => p.Name.Equals(select, StringComparison.OrdinalIgnoreCase)))
            ProfileBox.SelectedItem = profiles.First(p => p.Name.Equals(select, StringComparison.OrdinalIgnoreCase)).Name;
        else if (profiles.Count > 0 && ProfileBox.SelectedIndex < 0)
            ProfileBox.SelectedIndex = 0;

        if (ProfileBox.SelectedItem is string selected)
        {
            var info = profiles.FirstOrDefault(p => p.Name.Equals(selected, StringComparison.OrdinalIgnoreCase));
            PresetStatus.Text = ModProfileService.CurrentSummary() +
                                (info is null ? "" : $"  ·  Selected: {info.Blurb}");
        }
        else
            PresetStatus.Text = ModProfileService.CurrentSummary();
    }

    private void RebuildBuiltInPresets()
    {
        BuiltInPresetsPanel.Children.Clear();
        foreach (var preset in ModProfileService.BuiltIn)
            BuiltInPresetsPanel.Children.Add(BuiltInPresetTile(preset));
    }

    private UIElement BuiltInPresetTile(ModPresetInfo preset)
    {
        var modern = ThemeService.IsModern;
        var tile = new System.Windows.Controls.Border
        {
            Width = 148,
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(modern ? 10 : 4),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = preset
        };
        tile.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "InputBrush");
        tile.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "BorderBrush");

        var stack = new System.Windows.Controls.StackPanel();
        stack.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = preset.Name,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        var blurb = new System.Windows.Controls.TextBlock
        {
            Text = preset.Blurb,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        blurb.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "MutedBrush");
        stack.Children.Add(blurb);
        tile.Child = stack;
        ThemeService.OnPress(tile, () => _ = ApplyBuiltInPresetAsync(preset));
        return tile;
    }

    private async Task ApplyBuiltInPresetAsync(ModPresetInfo preset)
    {
        var ok = System.Windows.MessageBox.Show(
            $"Apply \"{preset.Name}\"?\n\n{preset.Blurb}\n\nYour current mods are saved as \"{ModProfileService.AutoSaveName}\" first.",
            AppInfo.Name, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ok != MessageBoxResult.Yes)
            return;

        await RunPresetAsync($"Applying {preset.Name}…", () => ModProfileService.ApplyBuiltIn(preset),
            $"Applied \"{preset.Name}\". Fully close the game, then Play again.",
            selectPreset: ModProfileService.AutoSaveName);
    }

    private async Task RunPresetAsync(string busy, Action work, string success, string? selectPreset = null)
    {
        PresetStatus.Text = busy;
        try
        {
            await Task.Run(work);
            RefreshProfiles(selectPreset);
            RefreshStatus(success);
            RefreshApplied();
            RebuildSlots();
            await RebuildSkyAsync();
            PresetStatus.Text = ModProfileService.CurrentSummary();
        }
        catch (Exception ex)
        {
            Logger.Error("Mods", ex);
            PresetStatus.Text = ex.Message;
            System.Windows.MessageBox.Show($"Could not apply that preset.\n\n{ex.Message}", AppInfo.Name,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
                "Remove every mod and put the client's original files back?\n\nYour current mods are saved as the preset \"Before last load\" first.",
                AppInfo.Name, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _ = RunPresetAsync("Removing mods…", ModProfileService.RemoveAllMods,
            "All mods removed and the original files restored.",
            selectPreset: ModProfileService.AutoSaveName);
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(ProfileNameBox.Text) ? ProfileBox.SelectedItem as string : ProfileNameBox.Text;
        try
        {
            var saved = ModProfileService.Save(name ?? "");
            ProfileNameBox.Text = "";
            RefreshProfiles(saved);
            RefreshStatus($"Saved your current mods (sky + everything) as \"{saved}\".");
            PresetStatus.Text = $"Saved \"{saved}\". " + ModProfileService.CurrentSummary();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Could not save the preset.\n\n{ex.Message}", AppInfo.Name,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not string name)
            return;

        _ = RunPresetAsync($"Loading {name}…", () => ModProfileService.Load(name),
            $"Loaded \"{name}\" and applied it to Octane. Fully close the game, then Play again.",
            selectPreset: name);
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not string name)
            return;
        if (System.Windows.MessageBox.Show($"Delete the preset \"{name}\"?", AppInfo.Name,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            ModProfileService.Delete(name);
            ProfileBox.SelectedIndex = -1;
            RefreshProfiles();
            RefreshStatus($"Deleted preset \"{name}\".");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Could not delete the preset.\n\n{ex.Message}", AppInfo.Name,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RebuildSlots()
    {
        RebuildSlotList(AtmosphereSlotsPanel, ModService.AtmosphereSlots());
        RebuildSlotList(SlotsPanel, ModService.NonAtmosphereSlots());
        ClearIndoorSkyButton.IsEnabled = ModService.HasIndoorSkyMods();
        AtmosphereStatus.Text = ModService.HasIndoorSkyMods()
            ? "Indoor sky mods are active."
            : "Indoor sky uses the client's stock cubemap unless you import faces.";
    }

    private void RebuildSlotList(System.Windows.Controls.Panel panel, IEnumerable<ModSlot> slots)
    {
        panel.Children.Clear();
        string? lastGroup = null;
        var list = slots.ToList();
        var showGroupHeaders = list.Select(slot => slot.Group).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1;
        foreach (var slot in list)
        {
            if (showGroupHeaders && slot.Group != lastGroup)
            {
                lastGroup = slot.Group;
                var header = new System.Windows.Controls.TextBlock
                {
                    Text = slot.Group.ToUpperInvariant(),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 14, 0, 8)
                };
                header.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "MutedBrush");
                panel.Children.Add(header);
            }

            panel.Children.Add(CreateRow(slot));
        }
    }

    private void ImportWater_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Folder of water textures (normal_01.dds, â€¦)",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        Run("Could not import water textures.", () =>
        {
            var count = ModService.ImportFlatFolder(dialog.SelectedPath, ModService.WaterRelativeFolder);
            RefreshStatus($"Imported {count} water texture(s). Fully close the game, then Play again.");
        });
    }

    private void ImportParticles_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Folder of particle textures",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        Run("Could not import particle textures.", () =>
        {
            var count = ModService.ImportFlatFolder(dialog.SelectedPath, ModService.ParticlesRelativeFolder);
            RefreshStatus($"Imported {count} particle texture(s). Fully close the game, then Play again.");
        });
    }

    private void ImportIndoorSky_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Folder with indoor512_*.tex (or bk/dn/ft/lf/rt/up)",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        Run("Could not import the indoor sky.", () =>
        {
            var count = ModService.ImportIndoorSkyFolder(dialog.SelectedPath);
            RefreshStatus($"Imported indoor sky ({count} face(s)). Fully close the game, then Play again.");
        });
    }

    private void ClearIndoorSky_Click(object sender, RoutedEventArgs e)
    {
        Run("Could not clear the indoor sky.", () =>
        {
            ModService.ClearIndoorSky();
            RefreshStatus("Indoor sky mods cleared; stock indoor cubemap will return on the next apply.");
        });
    }

    private void ReplaceClientFile_Click(object sender, RoutedEventArgs e)
    {
        var install = ClientLocator.Find(App.Settings.Prop, App.State.Prop);
        if (install is null || string.IsNullOrWhiteSpace(install.VersionDirectory) || !Directory.Exists(install.VersionDirectory))
        {
            System.Windows.MessageBox.Show(
                "X Bootstrapper could not find an Octane client folder yet.\n\nInstall Octane from octane.wtf once, then try again.",
                AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        using var targetDialog = new OpenFileDialog
        {
            Title = "Step 1 â€” pick the Octane client file to override",
            InitialDirectory = install.VersionDirectory,
            CheckFileExists = true,
            Filter = "Client assets|*.dds;*.png;*.jpg;*.jpeg;*.tex;*.mp3;*.ogg;*.wav;*.ttf;*.otf;*.particle|All files|*.*"
        };
        if (targetDialog.ShowDialog() != DialogResult.OK)
            return;

        string relative;
        try
        {
            relative = ModService.ClientRelativePath(targetDialog.FileName, install);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Could not use that client file.\n\n{ex.Message}", AppInfo.Name,
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        using var sourceDialog = new OpenFileDialog
        {
            Title = $"Step 2 — choose the replacement for {Path.GetFileName(relative)}",
            CheckFileExists = true,
            Filter = "All files|*.*|Textures|*.dds;*.png;*.jpg;*.jpeg;*.tex"
        };
        if (sourceDialog.ShowDialog() != DialogResult.OK)
            return;

        Run("Could not replace that client file.", () =>
        {
            ModService.SetClientRelativeFromFile(sourceDialog.FileName, relative);
            RefreshStatus($"Replaced {relative}. Fully close the game, then Play again.");
            AtmosphereStatus.Text = $"Last replace: {relative}";
        });
    }

    private UIElement CreateRow(ModSlot slot)
    {
        var installed = ModService.HasSlot(slot);
        var fileName = installed ? Path.GetFileName(ModService.SlotPath(slot)) : null;
        var path = ModPreviewService.ResolvePath(slot);

        var choose = new System.Windows.Controls.Button
        {
            Content = installed ? "Replace" : "Choose file",
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

        if (ModPreviewService.IsAudio(slot))
        {
            var play = new System.Windows.Controls.Button
            {
                Content = path is not null && ModAudioPreview.IsPlaying(path) ? "Stop" : "Play",
                Style = (Style)FindResource("GhostButton"),
                Margin = new Thickness(0, 0, 8, 0),
                IsEnabled = path is not null,
                Tag = slot,
                ToolTip = path is null ? "Choose a sound first (or wait until the stock file is found)." : "Preview this sound"
            };
            play.Click += PlaySlot_Click;
            buttons.Children.Add(play);
        }

        var previewBtn = new System.Windows.Controls.Button
        {
            Content = "Preview",
            Style = (Style)FindResource("GhostButton"),
            Margin = new Thickness(0, 0, 8, 0),
            Tag = slot,
            ToolTip = ModPreviewService.IsParticle(slot)
                ? "Open a particle-field preview"
                : ModPreviewService.IsAudio(slot)
                    ? "Open the sound preview"
                    : "Open a larger preview"
        };
        previewBtn.Click += PreviewSlot_Click;
        buttons.Children.Add(previewBtn);
        buttons.Children.Add(choose);
        buttons.Children.Add(remove);

        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

        var thumbBorder = new System.Windows.Controls.Border
        {
            Width = 52,
            Height = 52,
            CornerRadius = new CornerRadius(ThemeService.IsModern ? 8 : 3),
            Margin = new Thickness(0, 0, 12, 0),
            ClipToBounds = true,
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = slot,
            ToolTip = "Click for a larger preview"
        };
        thumbBorder.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "InputBrush");
        var thumb = new System.Windows.Controls.Image
        {
            Stretch = System.Windows.Media.Stretch.UniformToFill,
            Source = ModPreviewService.LoadThumb(slot, 52) ?? ModPreviewService.Placeholder(52, ModPreviewService.IsAudio(slot) ? "♪" : "—")
        };
        RenderOptions.SetBitmapScalingMode(thumb, System.Windows.Media.BitmapScalingMode.HighQuality);
        thumbBorder.Child = thumb;
        ThemeService.OnPress(thumbBorder, () => OpenPreview(slot));

        // Lightweight particle motion on the particle slot thumb
        if (ModPreviewService.IsParticle(slot) && path is not null)
            AttachParticleThumbMotion(thumbBorder, thumb);

        var labels = new System.Windows.Controls.StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(new System.Windows.Controls.TextBlock { Text = slot.Title, FontWeight = FontWeights.SemiBold });
        var hint = new System.Windows.Controls.TextBlock
        {
            Text = installed
                ? $"Using {fileName}"
                : path is not null
                    ? $"Stock preview · {slot.Hint}"
                    : slot.Hint,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        hint.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "MutedBrush");
        labels.Children.Add(hint);

        System.Windows.Controls.Grid.SetColumn(thumbBorder, 0);
        System.Windows.Controls.Grid.SetColumn(labels, 1);
        System.Windows.Controls.Grid.SetColumn(buttons, 2);
        grid.Children.Add(thumbBorder);
        grid.Children.Add(labels);
        grid.Children.Add(buttons);
        return grid;
    }

    private void AttachParticleThumbMotion(System.Windows.Controls.Border host, System.Windows.Controls.Image thumb)
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        var t = 0.0;
        timer.Tick += (_, _) =>
        {
            if (!IsLoaded || !host.IsVisible)
            {
                timer.Stop();
                return;
            }

            t += 0.04;
            var ox = Math.Sin(t * 2.1) * 3;
            var oy = Math.Cos(t * 1.7) * 2;
            thumb.RenderTransform = new System.Windows.Media.TranslateTransform(ox, oy);
            thumb.Opacity = 0.72 + 0.28 * (0.5 + 0.5 * Math.Sin(t * 3.2));
        };
        host.Loaded += (_, _) => timer.Start();
        host.Unloaded += (_, _) => timer.Stop();
    }

    private void PlaySlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: ModSlot slot })
            return;
        var path = ModPreviewService.ResolvePath(slot);
        if (path is null)
            return;
        ModAudioPreview.Toggle(path);
        RebuildSlots();
    }

    private void PreviewSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: ModSlot slot })
            OpenPreview(slot);
    }

    private void OpenPreview(ModSlot slot)
    {
        try
        {
            var window = new ModPreviewWindow(slot) { Owner = Window.GetWindow(this) };
            window.ShowDialog();
            RebuildSlots();
        }
        catch (Exception ex)
        {
            Logger.Error("ModPreview", ex);
            System.Windows.MessageBox.Show($"Could not open the preview.\n\n{ex.Message}", AppInfo.Name,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
