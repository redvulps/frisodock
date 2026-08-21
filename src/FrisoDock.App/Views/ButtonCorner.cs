using System.Windows;

namespace FrisoDock.App.Views;

/// <summary>
/// Corner radius of a button, for the button's own template to read.
///
/// <see cref="System.Windows.Controls.Button"/> has no <c>CornerRadius</c>, and rounding a
/// Border around it does not help: the WPF Border does not clip its children by
/// <c>CornerRadius</c> — and <c>ClipToBounds</c> clips by the rectangle, not by the rounded
/// shape. The button background keeps being painted into the corner, square.
///
/// So the radius has to reach the Border inside the template, and that is what this property
/// carries. It is attached, and not a style per variant, because split tiles need different radii
/// (left and right) over the same template.
/// </summary>
public static class ButtonCorner
{
    public static readonly DependencyProperty RadiusProperty =
        DependencyProperty.RegisterAttached(
            "Radius",
            typeof(CornerRadius),
            typeof(ButtonCorner),
            new PropertyMetadata(default(CornerRadius)));

    public static CornerRadius GetRadius(DependencyObject element)
    {
        return (CornerRadius)element.GetValue(RadiusProperty);
    }

    public static void SetRadius(DependencyObject element, CornerRadius value)
    {
        element.SetValue(RadiusProperty, value);
    }
}
