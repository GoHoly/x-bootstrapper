using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Caelus.Core;

namespace Caelus.UI;

/// <summary>
/// "What's new" after an update: this version's release notes, bundled into the app at build time
/// (release-notes.md), so it works offline. Shown once per version; "Don't show again" turns it off.
/// </summary>
public partial class WhatsNewWindow : Window
{
    public WhatsNewWindow()
    {
        InitializeComponent();
        TitleText.Text = $"What's new in {AppInfo.Version}";
        SubtitleText.Text = $"{AppInfo.Name} was updated. Here's what changed.";
        DontShowBox.IsChecked = !App.Settings.Prop.ShowWhatsNew;
        DontShowBox.Checked += (_, _) => SetShow(false);
        DontShowBox.Unchecked += (_, _) => SetShow(true);
        Render(BundledNotes());
        Closed += (_, _) => App.RequestExitIfIdle();
    }

    /// <summary>Opens the popup over the menu. <paramref name="markShown"/>: count it as this version's one showing.</summary>
    public static WhatsNewWindow ShowFor(Window? owner, bool markShown)
    {
        var existing = Application.Current.Windows.OfType<WhatsNewWindow>().FirstOrDefault();
        if (existing is not null)
        {
            existing.Activate();
            return existing;
        }

        var window = new WhatsNewWindow();
        if (owner is { IsVisible: true })
            window.Owner = owner;
        else
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.Show();
        if (markShown)
        {
            App.MarkWhatsNewShown();
            Logger.Write("App", $"Showed What's new for {AppInfo.Version}");
        }

        return window;
    }

    /// <summary>The release notes compiled into this build, or null if they are missing.</summary>
    public static string? BundledNotes()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("release-notes.md");
            if (stream is null)
                return null;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }

    private static void SetShow(bool show)
    {
        App.Settings.Prop.ShowWhatsNew = show;
        App.Save();
    }

    private void Render(string? markdown)
    {
        NotesPanel.Children.Clear();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            NotesPanel.Children.Add(Paragraph("The notes for this version aren't included in this build. Open the release page to read them."));
            return;
        }

        foreach (var raw in markdown.Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0)
                continue;

            if (line.StartsWith("#"))
            {
                var heading = new TextBlock
                {
                    Text = line.TrimStart('#', ' '),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, NotesPanel.Children.Count == 0 ? 0 : 14, 0, 6)
                };
                heading.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                NotesPanel.Children.Add(heading);
            }
            else if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
            {
                var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                var dot = new TextBlock { Text = "•", FontWeight = FontWeights.Bold };
                dot.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                var text = Rich(line.TrimStart()[2..]);
                Grid.SetColumn(text, 1);
                grid.Children.Add(dot);
                grid.Children.Add(text);
                NotesPanel.Children.Add(grid);
            }
            else
            {
                NotesPanel.Children.Add(Paragraph(line));
            }
        }
    }

    private static TextBlock Paragraph(string text)
    {
        var block = Rich(text);
        block.Margin = new Thickness(0, 0, 0, 8);
        return block;
    }

    /// <summary>A wrapped TextBlock with **bold** runs; `code` marks and links are shown as plain text.</summary>
    private static TextBlock Rich(string text)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 19 };
        text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]+\)", "$1").Replace("`", "");
        var parts = text.Split("**");
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
                continue;
            var run = new Run(parts[i]);
            if (i % 2 == 1)
                run.FontWeight = FontWeights.SemiBold;
            else
                run.SetResourceReference(TextElement.ForegroundProperty, "MutedBrush");
            block.Inlines.Add(run);
        }

        return block;
    }

    private void Release_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = $"{AppInfo.GitHubUrl}/releases/tag/v{AppInfo.Version}", UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Write("App", $"Could not open the release page: {ex.Message}");
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
