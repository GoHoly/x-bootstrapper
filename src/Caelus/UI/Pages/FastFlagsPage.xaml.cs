using System.Windows;
using System.Windows.Controls;
using Caelus.Core;
using Caelus.Models;
using Caelus.Services;

namespace Caelus.UI.Pages;

public partial class FastFlagsPage : System.Windows.Controls.UserControl
{
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
        FlagsBox.Text = string.Join(Environment.NewLine, s.FastFlags.Select(kv => $"{kv.Key}={kv.Value}"));

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
        System.Windows.MessageBox.Show(
            "Flags are saved into the Octane ClientSettings folder. Rejoin the game for them to take effect.",
            AppInfo.Name);
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
            var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in FlagsBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
                    flags[parts[0].Trim()] = parts[1].Trim();
            }

            s.FastFlags = flags;
        }

        App.Save();
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
