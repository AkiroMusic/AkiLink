using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AkiLink.Controls;

/// <summary>
/// Border variant with an inset hairline ("double frame") per the Ethereal
/// Glass spec: the outer frame uses the standard BorderBrush/BorderThickness/
/// CornerRadius properties, and OnRender adds a second thin line inset by
/// <see cref="Inset"/> pixels with a corner radius of (CornerRadius - Inset).
/// The inner line is drawn behind the child content, giving cards a refined
/// recessed edge without extra XAML layers.
/// </summary>
public sealed class CardBorder : Border
{
    public static readonly DependencyProperty InsetProperty = DependencyProperty.Register(
        nameof(Inset),
        typeof(double),
        typeof(CardBorder),
        new FrameworkPropertyMetadata(5.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Distance in pixels between the outer frame and the inner hairline. Default 5.</summary>
    public double Inset
    {
        get => (double)GetValue(InsetProperty);
        set => SetValue(InsetProperty, value);
    }

    public static readonly DependencyProperty InnerLineBrushProperty = DependencyProperty.Register(
        nameof(InnerLineBrush),
        typeof(Brush),
        typeof(CardBorder),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Brush for the inner hairline. When null, falls back to the "GlassInnerLine"
    /// application resource (rgba(255,255,255,0.05)).
    /// </summary>
    public Brush? InnerLineBrush
    {
        get => (Brush?)GetValue(InnerLineBrushProperty);
        set => SetValue(InnerLineBrushProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var brush = InnerLineBrush ?? (Brush?)Application.Current.TryFindResource("GlassInnerLine");
        if (brush is null || Inset <= 0)
            return;

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0)
            return;

        double radius = Math.Max(0, CornerRadius.TopLeft - Inset);
        double innerW = w - (Inset * 2);
        double innerH = h - (Inset * 2);
        if (innerW <= 0 || innerH <= 0)
            return;

        var pen = new Pen(brush, 1.0);
        pen.Freeze();

        // Align the hairline to pixel centers so it renders crisply.
        double half = 0.5;
        var guides = new GuidelineSet();
        guides.GuidelinesX.Add(Inset + half);
        guides.GuidelinesX.Add(Inset + innerW - half);
        guides.GuidelinesY.Add(Inset + half);
        guides.GuidelinesY.Add(Inset + innerH - half);

        dc.PushGuidelineSet(guides);
        var geometry = new RectangleGeometry(
            new Rect(Inset + half, Inset + half, innerW - 1, innerH - 1),
            radius,
            radius);
        dc.DrawGeometry(null, pen, geometry);
        dc.Pop();
    }
}
