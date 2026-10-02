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
using Color = System.Windows.Media.Color;

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

    // Classic candy-corn bands + Halloween button orange for wrappers / lollipops.
    private static readonly Color CandyWhite = Color.FromRgb(0xFF, 0xF6, 0xE8);
    private static readonly Color CandyYellow = Color.FromRgb(0xFF, 0xD0, 0x3A);
    private static readonly Color CandyStick = Color.FromRgb(0xE8, 0xD8, 0xC0);

    /// <summary>
    /// Halloween click burst: recognizable candy (corn, wrapped sweets, lollipops, jelly beans)
    /// with pumpkin orange as the main wrapper / pop color.
    /// </summary>
    private static void CandyBurst(Canvas layer, Point origin)
    {
        var orange = ThemeService.Current.Accent;
        var orangeHot = ThemeService.Current.AccentHover;

        var count = Rng.Next(4, 7);
        for (var i = 0; i < count; i++)
        {
            FrameworkElement piece = Rng.Next(4) switch
            {
                0 => MakeCandyCorn(),
                1 => MakeWrappedCandy(orange, orangeHot),
                2 => MakeLollipop(orange, orangeHot),
                _ => MakeJellyBean(orange, orangeHot)
            };

            AnimateParticle(layer, piece, origin, i, dropChance: 0.35, spinRange: 100);
        }
    }

    private static SolidColorBrush FreezeBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static System.Windows.Media.Effects.DropShadowEffect SoftGlow(Color color) =>
        new()
        {
            Color = color,
            BlurRadius = 10,
            ShadowDepth = 0,
            Opacity = 0.75
        };

    /// <summary>White tip → orange band → yellow base (classic candy corn).</summary>
    private static FrameworkElement MakeCandyCorn()
    {
        var h = 16 + Rng.Next(8);
        var w = h * 0.72;
        var canvas = new Canvas
        {
            Width = w,
            Height = h,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(),
            Effect = SoftGlow(ThemeService.Current.Accent)
        };

        // Base (yellow)
        canvas.Children.Add(new Polygon
        {
            Points = new PointCollection { new(w * 0.5, 0), new(w, h), new(0, h) },
            Fill = FreezeBrush(CandyYellow)
        });
        // Mid (pumpkin orange)
        canvas.Children.Add(new Polygon
        {
            Points = new PointCollection
            {
                new(w * 0.5, 0),
                new(w * 0.82, h * 0.62),
                new(w * 0.18, h * 0.62)
            },
            Fill = FreezeBrush(ThemeService.Current.Accent)
        });
        // Tip (cream white)
        canvas.Children.Add(new Polygon
        {
            Points = new PointCollection
            {
                new(w * 0.5, 0),
                new(w * 0.68, h * 0.28),
                new(w * 0.32, h * 0.28)
            },
            Fill = FreezeBrush(CandyWhite)
        });
        return canvas;
    }

    /// <summary>Hard candy with twisted foil ends and a light stripe down the middle.</summary>
    private static FrameworkElement MakeWrappedCandy(Color orange, Color orangeHot)
    {
        var bodyW = 14 + Rng.Next(8);
        var bodyH = 8 + Rng.Next(4);
        var twist = 6.0;
        var totalW = bodyW + twist * 2;
        var canvas = new Canvas
        {
            Width = totalW,
            Height = bodyH,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(),
            Effect = SoftGlow(orange)
        };

        var wrap = FreezeBrush(Rng.Next(2) == 0 ? orange : orangeHot);
        var foil = FreezeBrush(CandyWhite);

        // Left / right twists (foil)
        canvas.Children.Add(new Polygon
        {
            Points = new PointCollection
            {
                new(0, bodyH * 0.5),
                new(twist, 0),
                new(twist, bodyH)
            },
            Fill = foil
        });
        canvas.Children.Add(new Polygon
        {
            Points = new PointCollection
            {
                new(totalW, bodyH * 0.5),
                new(totalW - twist, 0),
                new(totalW - twist, bodyH)
            },
            Fill = foil
        });

        // Body
        var body = new System.Windows.Shapes.Rectangle
        {
            Width = bodyW,
            Height = bodyH,
            RadiusX = bodyH / 2,
            RadiusY = bodyH / 2,
            Fill = wrap
        };
        Canvas.SetLeft(body, twist);
        canvas.Children.Add(body);

        // Cream stripe so it reads as wrapped candy, not a pill
        var stripe = new System.Windows.Shapes.Rectangle
        {
            Width = bodyW * 0.22,
            Height = bodyH * 0.72,
            RadiusX = 2,
            RadiusY = 2,
            Fill = foil,
            Opacity = 0.9
        };
        Canvas.SetLeft(stripe, twist + bodyW * 0.39);
        Canvas.SetTop(stripe, bodyH * 0.14);
        canvas.Children.Add(stripe);

        return canvas;
    }

    /// <summary>Round pop with a white swirl and a short stick.</summary>
    private static FrameworkElement MakeLollipop(Color orange, Color orangeHot)
    {
        var pop = 12 + Rng.Next(6);
        var stickH = 8 + Rng.Next(4);
        var canvas = new Canvas
        {
            Width = pop,
            Height = pop + stickH,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(),
            Effect = SoftGlow(orange)
        };

        var stick = new System.Windows.Shapes.Rectangle
        {
            Width = 2.5,
            Height = stickH + 2,
            RadiusX = 1,
            RadiusY = 1,
            Fill = FreezeBrush(CandyStick)
        };
        Canvas.SetLeft(stick, (pop - 2.5) / 2);
        Canvas.SetTop(stick, pop * 0.7);
        canvas.Children.Add(stick);

        var head = new Ellipse
        {
            Width = pop,
            Height = pop,
            Fill = FreezeBrush(Rng.Next(2) == 0 ? orange : orangeHot)
        };
        canvas.Children.Add(head);

        // Swirl highlight
        var shine = new Ellipse
        {
            Width = pop * 0.38,
            Height = pop * 0.38,
            Fill = FreezeBrush(CandyWhite),
            Opacity = 0.85
        };
        Canvas.SetLeft(shine, pop * 0.22);
        Canvas.SetTop(shine, pop * 0.18);
        canvas.Children.Add(shine);

        // Thin ring so it reads as a candy pop
        var ring = new Ellipse
        {
            Width = pop * 0.72,
            Height = pop * 0.72,
            Stroke = FreezeBrush(CandyWhite),
            StrokeThickness = 1.4,
            Fill = Brushes.Transparent,
            Opacity = 0.55
        };
        Canvas.SetLeft(ring, pop * 0.14);
        Canvas.SetTop(ring, pop * 0.14);
        canvas.Children.Add(ring);

        return canvas;
    }

    /// <summary>Speckled jelly bean — oval with a glossy highlight.</summary>
    private static FrameworkElement MakeJellyBean(Color orange, Color orangeHot)
    {
        var w = 12 + Rng.Next(6);
        var h = 8 + Rng.Next(4);
        var canvas = new Canvas
        {
            Width = w,
            Height = h,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(),
            Effect = SoftGlow(orange)
        };

        canvas.Children.Add(new Ellipse
        {
            Width = w,
            Height = h,
            Fill = FreezeBrush(Rng.Next(2) == 0 ? orange : orangeHot)
        });

        var shine = new Ellipse
        {
            Width = w * 0.35,
            Height = h * 0.28,
            Fill = FreezeBrush(CandyWhite),
            Opacity = 0.8
        };
        Canvas.SetLeft(shine, w * 0.22);
        Canvas.SetTop(shine, h * 0.18);
        canvas.Children.Add(shine);

        // Tiny darker speckles (reads as candy coating, not a plain blob)
        var speck = FreezeBrush(Color.FromArgb(0x55, 0x80, 0x30, 0x00));
        for (var s = 0; s < 3; s++)
        {
            var dot = new Ellipse { Width = 1.6, Height = 1.6, Fill = speck };
            Canvas.SetLeft(dot, w * (0.35 + Rng.NextDouble() * 0.4));
            Canvas.SetTop(dot, h * (0.35 + Rng.NextDouble() * 0.4));
            canvas.Children.Add(dot);
        }

        return canvas;
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
