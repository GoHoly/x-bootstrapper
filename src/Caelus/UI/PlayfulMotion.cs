using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Caelus.Models;
using FontFamily = System.Windows.Media.FontFamily;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;
using RadioButton = System.Windows.Controls.RadioButton;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Button = System.Windows.Controls.Button;
using Border = System.Windows.Controls.Border;
using Canvas = System.Windows.Controls.Canvas;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace Caelus.UI;

internal static class PlayfulMotion
{
    private static readonly string[] XyxyEmojis =
    {
        "✨", "💕", "🌸", "🎀", "⭐", "💫", "💗", "💖", "🧁", "🍓", "💎", "🌷"
    };

    private static readonly Random Rng = new();

    public static bool IsPlayful => ThemeService.Current.Playful;

    public static bool IsHalloween => ThemeService.Current.Id == AppTheme.Halloween;

    public static void Attach(Window window, Canvas layer)
    {
        window.PreviewMouseLeftButtonDown += (_, e) => OnPress(layer, e);
        window.Loaded += (_, _) => FadeIn(window);
        if (window.IsLoaded)
            FadeIn(window);
    }

    public static void FadeIn(UIElement element, int ms = 220)
    {
        element.Opacity = 0;
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    public static void PopIn(FrameworkElement element)
    {
        if (element.RenderTransform is not ScaleTransform)
        {
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = new ScaleTransform(0.96, 0.96);
        }

        var scale = (ScaleTransform)element.RenderTransform;
        var to = 1d;
        var from = IsPlayful ? 0.9 : 0.97;
        var duration = TimeSpan.FromMilliseconds(IsPlayful ? 280 : 180);
        var ease = new BackEase { Amplitude = IsPlayful ? 0.55 : 0.25, EasingMode = EasingMode.EaseOut };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(from, to, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(from, to, duration) { EasingFunction = ease });
        FadeIn(element, IsPlayful ? 240 : 160);
    }

    private static void OnPress(Canvas layer, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject origin)
            return;

        var interactive = FindInteractive(origin);
        if (interactive is null)
            return;

        Bounce(interactive);
        UiSound.PlayClick();

        if (!IsPlayful)
            return;

        Burst(layer, e.GetPosition(layer));
    }

    private static void Bounce(FrameworkElement element)
    {
        if (element.RenderTransform is not ScaleTransform)
        {
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = new ScaleTransform(1, 1);
        }

        var scale = (ScaleTransform)element.RenderTransform;
        var down = IsPlayful ? 0.9 : 0.97;
        var back = TimeSpan.FromMilliseconds(IsPlayful ? 240 : 150);
        var drop = TimeSpan.FromMilliseconds(70);
        var ease = new BackEase { Amplitude = IsPlayful ? 0.8 : 0.3, EasingMode = EasingMode.EaseOut };

        var x = new DoubleAnimationUsingKeyFrames();
        x.KeyFrames.Add(new EasingDoubleKeyFrame(down, KeyTime.FromTimeSpan(drop)));
        x.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(drop + back)) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, x);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, x.Clone());
    }

    private static void Burst(Canvas layer, Point origin)
    {
        if (IsHalloween)
        {
            CandyBurst(layer, origin);
            return;
        }

        var count = Rng.Next(5, 8);
        for (var i = 0; i < count; i++)
        {
            var emoji = new TextBlock
            {
                Text = XyxyEmojis[Rng.Next(XyxyEmojis.Length)],
                FontSize = Rng.Next(15, 24),
                FontFamily = new FontFamily("Segoe UI Emoji"),
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform()
            };
            AnimateParticle(layer, emoji, origin, i, dropChance: 0, spinRange: 40);
        }
    }

    /// <summary>
    /// Halloween click burst: candy shapes in the same orange as AccentButton / AccentHover.
    /// </summary>
    private static void CandyBurst(Canvas layer, Point origin)
    {
        var accent = ThemeService.Current.Accent;
        var hover = ThemeService.Current.AccentHover;
        // Match the Halloween buttons exactly — accent + hover only (no cream/amber drift).
        var colors = new[] { accent, accent, accent, hover, hover };

        var count = Rng.Next(7, 11);
        for (var i = 0; i < count; i++)
        {
            var color = colors[Rng.Next(colors.Length)];
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var glow = new SolidColorBrush(Color.FromArgb(0x66, color.R, color.G, color.B));
            glow.Freeze();

            FrameworkElement piece = Rng.Next(3) switch
            {
                0 => CandyCorn(brush, glow),
                1 => WrappedCandy(brush, glow),
                _ => SparkleChip(brush, glow)
            };

            AnimateParticle(layer, piece, origin, i, dropChance: 0.4, spinRange: 80);
        }
    }

    private static FrameworkElement CandyCorn(Brush fill, Brush glow)
    {
        var size = 10 + Rng.Next(8);
        var poly = new Polygon
        {
            Points = new PointCollection
            {
                new(size * 0.5, 0),
                new(size, size),
                new(0, size)
            },
            Fill = fill,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = ((SolidColorBrush)glow).Color,
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.85
            },
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform()
        };
        return poly;
    }

    private static FrameworkElement WrappedCandy(Brush fill, Brush glow)
    {
        var w = 12 + Rng.Next(10);
        var h = 7 + Rng.Next(5);
        var grid = new Grid
        {
            Width = w + 8,
            Height = h,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform()
        };
        // Twists
        grid.Children.Add(new Polygon
        {
            Points = new PointCollection { new(0, h * 0.5), new(5, 0), new(5, h) },
            Fill = fill,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        grid.Children.Add(new Polygon
        {
            Points = new PointCollection { new(0, 0), new(0, h), new(5, h * 0.5) },
            Fill = fill,
            HorizontalAlignment = HorizontalAlignment.Right
        });
        var body = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(h / 2),
            Background = fill,
            HorizontalAlignment = HorizontalAlignment.Center,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = ((SolidColorBrush)glow).Color,
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.9
            }
        };
        grid.Children.Add(body);
        return grid;
    }

    private static FrameworkElement SparkleChip(Brush fill, Brush glow)
    {
        var size = 6 + Rng.Next(8);
        return new Ellipse
        {
            Width = size,
            Height = size,
            Fill = fill,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = ((SolidColorBrush)glow).Color,
                BlurRadius = 12,
                ShadowDepth = 0,
                Opacity = 0.95
            },
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform()
        };
    }

    private static void AnimateParticle(Canvas layer, FrameworkElement piece, Point origin, int index, double dropChance, int spinRange)
    {
        var startX = origin.X + Rng.Next(-18, 18);
        var startY = origin.Y - 6;
        Canvas.SetLeft(piece, startX);
        Canvas.SetTop(piece, startY);
        Panel.SetZIndex(piece, 80);
        layer.Children.Add(piece);

        var duration = TimeSpan.FromMilliseconds(620 + Rng.Next(220));
        var delay = TimeSpan.FromMilliseconds(index * 18);
        var drop = dropChance > 0 && Rng.NextDouble() < dropChance;
        var endY = drop ? startY + 36 + Rng.Next(40) : startY - 48 - Rng.Next(40);
        var endX = startX + Rng.Next(-42, 42);

        var moveY = new DoubleAnimation(startY, endY, duration)
        {
            BeginTime = delay,
            EasingFunction = drop
                ? new QuadraticEase { EasingMode = EasingMode.EaseIn }
                : new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var moveX = new DoubleAnimation(startX, endX, duration)
        {
            BeginTime = delay,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var fade = new DoubleAnimation(1, 0, duration)
        {
            BeginTime = delay,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        var spin = new DoubleAnimation(0, Rng.Next(-spinRange, spinRange), duration) { BeginTime = delay };

        fade.Completed += (_, _) => layer.Children.Remove(piece);
        piece.BeginAnimation(Canvas.TopProperty, moveY);
        piece.BeginAnimation(Canvas.LeftProperty, moveX);
        piece.BeginAnimation(UIElement.OpacityProperty, fade);
        if (piece.RenderTransform is RotateTransform rotate)
            rotate.BeginAnimation(RotateTransform.AngleProperty, spin);
    }

    private static FrameworkElement? FindInteractive(DependencyObject start)
    {
        for (var current = start; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            switch (current)
            {
                case System.Windows.Controls.Button button:
                    return button;
                case RadioButton radio:
                    return radio;
                case CheckBox check:
                    return check;
                case ComboBox combo:
                    return combo;
                case Border { Tag: Models.AppTheme, Cursor: not null } tile:
                    return tile;
            }

            if (current is Window)
                break;
        }

        return null;
    }
}
