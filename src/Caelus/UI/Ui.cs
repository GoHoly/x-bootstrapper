using System.Windows;

namespace Caelus.UI;

/// <summary>
/// Attached properties used by the style dictionaries. The Modern templates read them; the Classic
/// templates ignore them, so setting one never changes the Classic look.
/// </summary>
public static class Ui
{
    /// <summary>Segoe Fluent Icons / Segoe MDL2 Assets glyph shown by Modern nav items and buttons.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
        "Icon", typeof(string), typeof(Ui), new FrameworkPropertyMetadata(null));

    public static string? GetIcon(DependencyObject element) => (string?)element.GetValue(IconProperty);

    public static void SetIcon(DependencyObject element, string? value) => element.SetValue(IconProperty, value);
}
