using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FontFamily = System.Windows.Media.FontFamily;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;
using RadioButton = System.Windows.Controls.RadioButton;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Button = System.Windows.Controls.Button;
using Border = System.Windows.Controls.Border;
using Canvas = System.Windows.Controls.Canvas;

namespace Caelus.UI;

internal static class PlayfulMotion
{
    private static readonly string[] Emojis =
    {
        "✨", "💕", "🌸", "🎀", "⭐", "💫", "💗", "💖", "🧁", "🍓", "💎", "🌷"
    };

    private static readonly Random Rng = new();

    public static bool IsPlayful => ThemeService.Current.Playful;

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
        var count = Rng.Next(5, 8);
        for (var i = 0; i < count; i++)
        {
            var emoji = new TextBlock
            {
                Text = Emojis[Rng.Next(Emojis.Length)],
                FontSize = Rng.Next(15, 24),
                FontFamily = new FontFamily("Segoe UI Emoji"),
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform()
            };

            var startX = origin.X + Rng.Next(-18, 18);
            var startY = origin.Y - 6;
            Canvas.SetLeft(emoji, startX);
            Canvas.SetTop(emoji, startY);
            Panel.SetZIndex(emoji, 80);
            layer.Children.Add(emoji);

            var duration = TimeSpan.FromMilliseconds(620 + Rng.Next(180));
            var delay = TimeSpan.FromMilliseconds(i * 18);
            var endY = startY - 48 - Rng.Next(36);
            var endX = startX + Rng.Next(-36, 36);

            var moveY = new DoubleAnimation(startY, endY, duration)
            {
                BeginTime = delay,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
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
            var spin = new DoubleAnimation(0, Rng.Next(-40, 40), duration) { BeginTime = delay };

            fade.Completed += (_, _) => layer.Children.Remove(emoji);
            emoji.BeginAnimation(Canvas.TopProperty, moveY);
            emoji.BeginAnimation(Canvas.LeftProperty, moveX);
            emoji.BeginAnimation(UIElement.OpacityProperty, fade);
            ((RotateTransform)emoji.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, spin);
        }
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
