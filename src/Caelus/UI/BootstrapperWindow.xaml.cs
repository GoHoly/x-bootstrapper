using System.Windows;
using System.Windows.Media.Animation;
using Caelus.Core;
using Caelus.Services;

namespace Caelus.UI;

public partial class BootstrapperWindow : Window
{
    private readonly CancellationTokenSource _cts = new();

    public BootstrapperWindow()
    {
        InitializeComponent();
        PlayfulMotion.Attach(this, SparkleLayer);
        if (PlayfulMotion.IsPlayful)
        {
            TitleText.Text = $"{AppInfo.Name} ✨";
            SubtitleText.Text = "Starting Octane 🎀";
        }

        if (App.Settings.Prop.BootstrapperStyle == Models.BootstrapperStyle.Classic)
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240));
            Foreground = System.Windows.Media.Brushes.Black;
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PlayfulMotion.PopIn(Logo);
        PulseAccent();

        var bootstrapper = new BootstrapperService(App.Settings.Prop, App.State.Prop, App.Args);
        bootstrapper.StatusChanged += status => Dispatcher.Invoke(() =>
            StatusText.Text = PlayfulMotion.IsPlayful ? status + " ✨" : status);
        bootstrapper.ProgressChanged += (value, indeterminate) => Dispatcher.Invoke(() =>
        {
            Progress.IsIndeterminate = indeterminate;
            if (!indeterminate)
                Progress.Value = Math.Clamp(value * 100, 0, 100);
        });

        try
        {
            var process = await bootstrapper.RunAsync(_cts.Token);
            App.Save();

            if (process is not null)
            {
                var loaded = !string.IsNullOrWhiteSpace(App.Args.ProtocolUri)
                    ? "Game loaded."
                    : "Octane loaded.";
                NotifyService.Show(AppInfo.Name, loaded);
            }

            if (process is not null && App.Settings.Prop.DiscordRichPresence && !string.IsNullOrWhiteSpace(App.Settings.Prop.DiscordClientId))
            {
                App.Discord = new DiscordService();
                if (App.Discord.Connect(App.Settings.Prop.DiscordClientId))
                {
                    var place = App.Args.ExtractPlaceId();
                    App.Discord.SetPresence(
                        place is null ? "Playing Octane" : $"Place {place}",
                        "2021 revival",
                        place);
                }

                App.Watch = new ProcessWatch { Process = process };
                try
                {
                    process.EnableRaisingEvents = true;
                    process.Exited += (_, _) => Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
                }
                catch (InvalidOperationException)
                {
                    // OctanePlayerLauncher started the player, so this Process object cannot raise Exited.
                }
                Hide();
                return;
            }

            if (App.Settings.Prop.StayOpenAfterLaunch)
            {
                Hide();
                return;
            }

            System.Windows.Application.Current.Shutdown();
        }
        catch (OperationCanceledException)
        {
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Logger.Error("Bootstrapper", ex);
            System.Windows.MessageBox.Show(ex.Message, AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            if (App.Current.Windows.OfType<MenuWindow>().Any())
                Close();
            else
                System.Windows.Application.Current.Shutdown();
        }
    }

    private void PulseAccent()
    {
        if (!PlayfulMotion.IsPlayful)
            return;

        var pulse = new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(700))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        AccentBar.BeginAnimation(UIElement.OpacityProperty, pulse);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        Close();
        if (!App.Current.Windows.OfType<MenuWindow>().Any())
            System.Windows.Application.Current.Shutdown();
    }
}
