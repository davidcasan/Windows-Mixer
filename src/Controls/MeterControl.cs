using System.Windows;
using System.Windows.Media;

namespace WinMixer.Controls;

/// <summary>Stereo LED peak meter (VU) with peak hold.</summary>
public sealed class MeterControl : FrameworkElement
{
    public static readonly DependencyProperty LevelLProperty = DependencyProperty.Register(
        nameof(LevelL), typeof(double), typeof(MeterControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => ((MeterControl)d).Hold(0, (double)e.NewValue)));
    public static readonly DependencyProperty LevelRProperty = DependencyProperty.Register(
        nameof(LevelR), typeof(double), typeof(MeterControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => ((MeterControl)d).Hold(1, (double)e.NewValue)));

    public double LevelL { get => (double)GetValue(LevelLProperty); set => SetValue(LevelLProperty, value); }
    public double LevelR { get => (double)GetValue(LevelRProperty); set => SetValue(LevelRProperty, value); }

    const int Segments = 30;
    readonly double[] _hold = new double[2];
    readonly DateTime[] _holdTime = new DateTime[2];

    void Hold(int ch, double v)
    {
        var now = DateTime.UtcNow;
        if (v >= _hold[ch]) { _hold[ch] = v; _holdTime[ch] = now; }
        else if ((now - _holdTime[ch]).TotalMilliseconds > 900) _hold[ch] = Math.Max(v, _hold[ch] - 0.02);
    }

    static Color SegColor(int i)
    {
        double t = (i + 1) / (double)Segments;   // 0..1, display range -48..0 dB
        if (t > 0.92) return Color.FromRgb(0xFF, 0x2D, 0x1F);
        if (t > 0.75) return Color.FromRgb(0xFF, 0xC4, 0x1F);
        return Color.FromRgb(0x3C, 0xE0, 0x5A);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 6 || h < 20) return;

        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0x08, 0x09, 0x0A)), new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 1), new Rect(0.5, 0.5, w - 1, h - 1), 3, 3);

        double pad = 3, gap = 3;
        double colW = (w - pad * 2 - gap) / 2;
        double segH = (h - pad * 2) / Segments;

        for (int ch = 0; ch < 2; ch++)
        {
            double level = ch == 0 ? LevelL : LevelR;
            int lit = (int)Math.Round(level * Segments);
            int holdSeg = (int)Math.Round(_hold[ch] * Segments) - 1;
            double x = pad + ch * (colW + gap);
            for (int i = 0; i < Segments; i++)
            {
                double y = h - pad - (i + 1) * segH;
                var col = SegColor(i);
                bool on = i < lit || i == holdSeg && holdSeg >= 0;
                if (on)
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(55, col.R, col.G, col.B)), null, new Rect(x - 1.5, y - 0.5, colW + 3, segH + 1));
                    dc.DrawRectangle(new SolidColorBrush(col), null, new Rect(x, y + 0.5, colW, Math.Max(1, segH - 1.6)));
                }
                else
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(46, col.R, col.G, col.B)), null, new Rect(x, y + 0.5, colW, Math.Max(1, segH - 1.6)));
                }
            }
        }
    }
}
