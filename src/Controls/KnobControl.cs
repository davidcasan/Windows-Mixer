using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WinMixer.Controls;

/// <summary>Rotary knob rendered with lighting, knurled rim, tick scale and lit value arc.</summary>
public sealed class KnobControl : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(KnobControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            null, (d, v) => Math.Clamp((double)v, 0, 1)));

    public static readonly DependencyProperty DimmedProperty = DependencyProperty.Register(
        nameof(Dimmed), typeof(bool), typeof(KnobControl),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool Dimmed { get => (bool)GetValue(DimmedProperty); set => SetValue(DimmedProperty, value); }

    public double ResetValue { get; set; } = 1.0;

    const double SweepDeg = 135;
    static readonly Color Amber = Color.FromRgb(0xFF, 0x9F, 0x1C);

    bool _drag;
    Point _start;
    double _startValue;

    public KnobControl() { Cursor = Cursors.SizeNS; Focusable = false; }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { Value = ResetValue; return; }
        _drag = true;
        _start = e.GetPosition(null);
        _startValue = Value;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_drag) return;
        double dy = _start.Y - e.GetPosition(null).Y;
        double k = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 0.0008 : 0.006;
        Value = _startValue + dy * k;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_drag) return;
        _drag = false;
        ReleaseMouseCapture();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        Value += Math.Sign(e.Delta) * (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 0.005 : 0.025);
        e.Handled = true;
    }

    static Point Polar(Point c, double r, double deg)
    {
        double a = deg * Math.PI / 180;
        return new Point(c.X + r * Math.Sin(a), c.Y - r * Math.Cos(a));
    }

    static StreamGeometry Arc(Point c, double r, double fromDeg, double toDeg)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(Polar(c, r, fromDeg), false, false);
            ctx.ArcTo(Polar(c, r, toDeg), new Size(r, r), 0, toDeg - fromDeg > 180, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        return g;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size < 20) return;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        double R = size / 2;
        double body = R * 0.66;
        double angle = -SweepDeg + 2 * SweepDeg * Value;
        double dim = Dimmed ? 0.45 : 1.0;

        dc.PushOpacity(dim);

        // ticks
        for (int i = 0; i <= 10; i++)
        {
            double deg = -SweepDeg + 270.0 * i / 10;
            bool lit = deg <= angle + 0.01;
            var pen = new Pen(new SolidColorBrush(lit ? Amber : Color.FromRgb(0x6C, 0x70, 0x76)), i % 5 == 0 ? 2 : 1.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(pen, Polar(c, R * 0.90, deg), Polar(c, R * (i % 5 == 0 ? 1.0 : 0.96), deg));
        }

        // value arc groove + lit arc
        double ra = R * 0.80;
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(0x0E, 0x0F, 0x11)), 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, Arc(c, ra, -SweepDeg, SweepDeg));
        if (Value > 0.003)
        {
            var glow = new Pen(new SolidColorBrush(Color.FromArgb(70, Amber.R, Amber.G, Amber.B)), 9) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawGeometry(null, glow, Arc(c, ra, -SweepDeg, angle));
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Amber), 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, Arc(c, ra, -SweepDeg, angle));
        }

        // drop shadow
        var shadow = new RadialGradientBrush(Color.FromArgb(190, 0, 0, 0), Colors.Transparent)
        { GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5 };
        shadow.GradientStops[0].Offset = 0.72;
        dc.DrawEllipse(shadow, null, new Point(c.X + 1, c.Y + body * 0.14), body * 1.28, body * 1.28);

        // outer bezel
        var bezel = new LinearGradientBrush(Color.FromRgb(0x8A, 0x8E, 0x94), Color.FromRgb(0x14, 0x15, 0x17), 90);
        dc.DrawEllipse(bezel, null, c, body, body);

        // knurled rim
        var knurl = new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), 1);
        var knurlHi = new Pen(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 1);
        for (int i = 0; i < 60; i++)
        {
            double d = i * 6 + angle * 0.0;
            dc.DrawLine(knurl, Polar(c, body * 0.90, d), Polar(c, body * 0.995, d));
            dc.DrawLine(knurlHi, Polar(c, body * 0.90, d + 2), Polar(c, body * 0.995, d + 2));
        }

        // cap
        double cap = body * 0.84;
        var capBrush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.35, 0.28),
            Center = new Point(0.45, 0.4),
            RadiusX = 0.85,
            RadiusY = 0.85
        };
        capBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x6A, 0x6E, 0x75), 0));
        capBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x32, 0x35, 0x3A), 0.55));
        capBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x14, 0x15, 0x18), 1));
        dc.DrawEllipse(capBrush, new Pen(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), 1.5), c, cap, cap);

        // concentric machining rings on the cap
        for (int i = 1; i <= 3; i++)
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)), 0.7), c, cap * (0.25 + 0.2 * i), cap * (0.25 + 0.2 * i));

        // top specular
        var spec = new LinearGradientBrush(Color.FromArgb(95, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90);
        dc.DrawEllipse(spec, null, new Point(c.X, c.Y - cap * 0.30), cap * 0.62, cap * 0.40);

        // indicator (groove shadow + lit line)
        var ia = Polar(c, cap * 0.34, angle); var ib = Polar(c, cap * 0.90, angle);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
            new Point(ia.X + 0.5, ia.Y + 1.2), new Point(ib.X + 0.5, ib.Y + 1.2));
        dc.DrawLine(new Pen(new SolidColorBrush(Dimmed ? Color.FromRgb(0x99, 0x99, 0x99) : Color.FromRgb(0xFF, 0xE2, 0xB0)), 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, ia, ib);

        dc.Pop();
    }
}
