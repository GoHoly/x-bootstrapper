using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Caelus.Core;
using Caelus.Services;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Image = System.Windows.Controls.Image;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace Caelus.UI;

/// <summary>Larger mods preview: static texture, font sample, or a simple particle field.</summary>
public sealed class ModPreviewWindow : Window
{
    private readonly DispatcherTimer? _timer;
    private readonly Canvas? _canvas;
    private readonly List<Particle> _particles = new();
    private readonly ImageSource? _sprite;
    private readonly Random _rng = new(7);

    private sealed class Particle
    {
        public double X, Y, Vx, Vy, Life, MaxLife, Size, Spin;
        public Image Image = null!;
    }

    public ModPreviewWindow(ModSlot slot)
    {
        Title = slot.Title + " preview";
        Width = 520;
        Height = 420;
        MinWidth = 400;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        SetResourceReference(BackgroundProperty, "BackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextBrush");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 13;

        var chrome = new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 36,
            GlassFrameThickness = new Thickness(0),
            ResizeBorderThickness = new Thickness(6)
        };
        System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome);

        var path = ModPreviewService.ResolvePath(slot);
        var frame = new Border();
        frame.SetResourceReference(StyleProperty, "WindowFrame");

        var root = new Grid();
        root.SetResourceReference(StyleProperty, "DialogBody");
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new DockPanel();
        var close = new Button { Content = "✕" };
        close.SetResourceReference(StyleProperty, "CloseButton");
        close.VerticalAlignment = VerticalAlignment.Top;
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(close, true);
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        var titles = new StackPanel { Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = slot.Title + " preview", FontSize = 17, FontWeight = FontWeights.SemiBold });
        var sub = new TextBlock
        {
            Text = path is null
                ? "No file yet — choose one on the Mods page, or this shows the stock client asset when present."
                : (ModService.HasSlot(slot) ? "Your mod" : "Stock client file") + " — " + Path.GetFileName(path),
            Margin = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        sub.SetResourceReference(StyleProperty, "CardText");
        titles.Children.Add(sub);
        header.Children.Add(titles);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var stage = new Border { Margin = new Thickness(0, 16, 0, 12), ClipToBounds = true };
        stage.SetResourceReference(StyleProperty, "Card");
        stage.Padding = new Thickness(0);

        if (ModPreviewService.IsAudio(slot))
        {
            var audio = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            audio.Children.Add(new TextBlock
            {
                Text = path is null ? "No sound file to play." : "Click Play to hear this sound.",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            });
            if (path is not null)
            {
                var play = new Button { Content = "Play / Stop", Width = 140 };
                play.SetResourceReference(StyleProperty, "AccentButton");
                play.Click += (_, _) => ModAudioPreview.Toggle(path);
                audio.Children.Add(play);
            }

            stage.Child = audio;
        }
        else if (ModPreviewService.IsParticle(slot) && path is not null)
        {
            _sprite = ModPreviewService.LoadImage(path, 64);
            _canvas = new Canvas
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 22, 30))
            };
            stage.Child = _canvas;
            Loaded += (_, _) => SpawnInitial();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += (_, _) => Tick();
            _timer.Start();
            Closed += (_, _) => _timer.Stop();
        }
        else
        {
            var image = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(16),
                Source = ModPreviewService.LoadThumb(slot, 256) ?? ModPreviewService.Placeholder(256, "—")
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            stage.Child = image;
        }

        Grid.SetRow(stage, 1);
        root.Children.Add(stage);

        var footer = new DockPanel();
        var done = new Button { Content = "Close", IsCancel = true, IsDefault = true };
        done.SetResourceReference(StyleProperty, "GhostButton");
        done.Click += (_, _) => Close();
        DockPanel.SetDock(done, Dock.Right);
        footer.Children.Add(done);
        var tip = new TextBlock
        {
            Text = ModPreviewService.IsParticle(slot)
                ? "Simple particle field — not a full Octane sim, just a feel for the sprite."
                : "Preview only. Apply still happens from the Mods page.",
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        tip.SetResourceReference(StyleProperty, "CardText");
        footer.Children.Add(tip);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        var stripe = new Border();
        stripe.SetResourceReference(StyleProperty, "AccentStripe");
        frame.Child = new Grid { Children = { stripe, root } };
        Content = frame;
    }

    private void SpawnInitial()
    {
        if (_canvas is null || _sprite is null)
            return;
        for (var i = 0; i < 28; i++)
            _particles.Add(MakeParticle(seed: true));
    }

    private Particle MakeParticle(bool seed)
    {
        var w = Math.Max(40, _canvas!.ActualWidth);
        var h = Math.Max(40, _canvas.ActualHeight);
        var size = 10 + _rng.NextDouble() * 28;
        var img = new Image
        {
            Source = _sprite,
            Width = size,
            Height = size,
            Opacity = 0.85,
            Stretch = Stretch.Uniform
        };
        var p = new Particle
        {
            Image = img,
            X = _rng.NextDouble() * w,
            Y = seed ? _rng.NextDouble() * h : h + size,
            Vx = (_rng.NextDouble() - 0.5) * 40,
            Vy = -20 - _rng.NextDouble() * 55,
            Life = 0,
            MaxLife = 1.4 + _rng.NextDouble() * 2.2,
            Size = size,
            Spin = (_rng.NextDouble() - 0.5) * 90
        };
        _canvas.Children.Add(img);
        Canvas.SetLeft(img, p.X);
        Canvas.SetTop(img, p.Y);
        return p;
    }

    private void Tick()
    {
        if (_canvas is null)
            return;
        var dt = 0.016;
        var w = Math.Max(40, _canvas.ActualWidth);
        var h = Math.Max(40, _canvas.ActualHeight);
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life += dt;
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            p.Vy += 12 * dt;
            var t = p.Life / p.MaxLife;
            p.Image.Opacity = t < 0.15 ? t / 0.15 : Math.Max(0, 1 - (t - 0.15) / 0.85);
            p.Image.RenderTransform = new RotateTransform(p.Spin * t, p.Size / 2, p.Size / 2);
            Canvas.SetLeft(p.Image, p.X);
            Canvas.SetTop(p.Image, p.Y);
            if (p.Life >= p.MaxLife || p.Y < -40 || p.X < -40 || p.X > w + 40)
            {
                _canvas.Children.Remove(p.Image);
                _particles.RemoveAt(i);
                _particles.Add(MakeParticle(seed: false));
            }
        }
    }
}
