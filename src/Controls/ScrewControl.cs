using System.Windows;
using System.Windows.Media;

namespace WinMixer.Controls;

/// <summary>Decorative slotted screw. Angle comes from the Tag (degrees) so they are not all identical.</summary>
public sealed class ScrewControl : FrameworkElement
{
    public ScrewControl() { Width = 11; Height = 11; IsHitTestVisible = false; }

    protected override void OnRender(DrawingContext dc)
    {
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        double r = Math.Min(ActualWidth, ActualHeight) / 2 - 0.5;
        var brush = new RadialGradientBrush { GradientOrigin = new Point(0.35, 0.3) };
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0xC8, 0xCB, 0xD0), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x6A, 0x6D, 0x72), 0.7));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x30, 0x32, 0x36), 1));
        dc.DrawEllipse(brush, new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), 1), c, r, r);

        double deg = Tag is IConvertible t ? Convert.ToDouble(t) : 35;
        double a = deg * Math.PI / 180;
        var d = new Vector(Math.Cos(a), Math.Sin(a)) * (r * 0.72);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(230, 10, 10, 10)), 1.6), c - d, c + d);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), 0.8), c - d + new Vector(0.5, 1), c + d + new Vector(0.5, 1));
    }
}
