using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinMixer.Core;

namespace WinMixer.Views;

public partial class OsdWindow : Window
{
    const int GWL_EXSTYLE = -20;
    const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
    const uint SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10;

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    readonly MixerEngine _engine;
    readonly DispatcherTimer _hide = new();
    bool _shown;

    public OsdWindow(MixerEngine engine)
    {
        _engine = engine;
        InitializeComponent();
        _hide.Tick += (_, _) => { _hide.Stop(); FadeTo(0, 250, () => Hide2()); };
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        };
    }

    void Hide2() { Hide(); _shown = false; }

    void FadeTo(double to, int ms, Action? done = null)
    {
        var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
        if (done != null) a.Completed += (_, _) => done();
        BeginAnimation(OpacityProperty, a);
    }

    public void Display(OsdInfo info)
    {
        var osd = _engine.Settings.Osd;
        if (!osd.Enabled) return;

        TitleText.Text = info.Title;
        ValueText.Text = info.Text;
        BarWell.Visibility = info.Fraction.HasValue ? Visibility.Visible : Visibility.Collapsed;
        ValueText.Foreground = info.Muted ? Brushes.IndianRed : (Brush)FindResource("LcdText");
        UpdateLayout();
        double inner = 320 - 2 * 16 - 3;
        Fill.Width = Math.Max(0, (info.Fraction ?? 0) * inner * (info.Muted ? 0 : 1));

        if (!_shown)
        {
            Opacity = 0;
            Show();
            _shown = true;
        }
        Reposition();
        FadeTo(1, 120);
        _hide.Stop();
        _hide.Interval = TimeSpan.FromMilliseconds(Math.Max(300, osd.DurationMs));
        _hide.Start();
    }

    void Reposition()
    {
        var osd = _engine.Settings.Osd;
        var screens = System.Windows.Forms.Screen.AllScreens;
        var sc = screens.FirstOrDefault(s => s.DeviceName == osd.Monitor) ?? System.Windows.Forms.Screen.PrimaryScreen ?? screens[0];
        var wa = sc.WorkingArea;
        var h = new WindowInteropHelper(this).Handle;

        // move onto the target monitor first so WPF picks that monitor's DPI, then measure
        SetWindowPos(h, IntPtr.Zero, wa.Left, wa.Top, 0, 0, SWP_NOZORDER | SWP_NOACTIVATE | 1 /*NOSIZE*/);
        UpdateLayout();
        GetWindowRect(h, out var r);
        int w = r.R - r.L, ht = r.B - r.T, margin = (int)(24 * VisualTreeHelper.GetDpi(this).DpiScaleX);

        int col = (int)osd.Position % 3, row = (int)osd.Position / 3;
        int x = col switch { 0 => wa.Left + margin, 1 => wa.Left + (wa.Width - w) / 2, _ => wa.Right - w - margin };
        int y = row switch { 0 => wa.Top + margin, 1 => wa.Top + (wa.Height - ht) / 2, _ => wa.Bottom - ht - margin };
        SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, SWP_NOZORDER | SWP_NOACTIVATE | 1);
    }
}
