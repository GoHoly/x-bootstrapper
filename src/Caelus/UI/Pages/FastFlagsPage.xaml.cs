using System.Windows;
using System.Windows.Controls;
using Caelus.Core;
using Caelus.Models;

namespace Caelus.UI.Pages;

public partial class FastFlagsPage : System.Windows.Controls.UserControl
{
    public FastFlagsPage()
    {
        InitializeComponent();
        var s = App.Settings.Prop;
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
        FlagsBox.Text = string.Join(Environment.NewLine, s.FastFlags.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
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

        var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in FlagsBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('=', 2);
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
                flags[parts[0].Trim()] = parts[1].Trim();
        }

        s.FastFlags = flags;
        App.Save();
        System.Windows.MessageBox.Show("FastFlags will be written on the next launch.", AppInfo.Name);
    }
}
