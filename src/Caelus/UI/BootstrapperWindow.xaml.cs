using System.Windows;
using System.Windows.Media.Animation;
using Caelus.Core;
using Caelus.Services;

namespace Caelus.UI;

public partial class BootstrapperWindow : Window
{
    private readonly CancellationTokenSource _cts = new();
    private readonly LaunchArgs _args;

    public BootstrapperWindow(LaunchArgs args)
    {
        _args = args;
        InitializeComponent();
        Closed += (_, _) => App.RequestExitIfIdle();
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

        var bootstrapper = new BootstrapperService(App.Settings.Prop, App.State.Prop, _args);
        bootstrapper.StatusChanged += status => Dispatcher.BeginInvoke(() =>
            StatusText.Text = PlayfulMotion.IsPlayful ? status + " ✨" : status);
        bootstrapper.ProgressChanged += (value, indeterminate) => Dispatcher.BeginInvoke(() =>
        {
            Progress.IsIndeterminate = indeterminate;
            if (!indeterminate)
                Progress.Value = Math.Clamp(value * 100, 0, 100);
        });

        try
        {
            var result = await bootstrapper.RunAsync(_cts.Token);
            App.Save();

            var studio = _args.Mode == LaunchMode.Studio;
            if (result.IsGame && result.Process is not null)
            {
                NotifyService.Show(AppInfo.Name, studio
                    ? "Octane Studio is starting."
                    : !string.IsNullOrWhiteSpace(_args.ProtocolUri) ? "Joining your game." : "Octane is starting.");
                App.StartSession(new GameSession(result.Process, _args.ExtractPlaceId(), studio));
            }
            else
            {
                result.Process?.Dispose();
            }

            var fromMenu = App.Current.Windows.OfType<MenuWindow>().Any();
            if (App.Settings.Prop.StayOpenAfterLaunch && !fromMenu)
                App.ShowMenu();

            Close();
        }
        catch (OperationCanceledException)
        {
            Close();
        }
        catch (Exception ex)
        {
            Logger.Error("Bootstrapper", ex);
            System.Windows.MessageBox.Show(ex.Message, AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
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
    }
}
