using System.Windows;
using System.Windows.Controls;
using WinMixer.Core;

namespace WinMixer.Views;

public partial class SettingsPanel : UserControl
{
    MixerEngine _engine = null!;
    bool _loading;
    readonly RadioButton[] _pos = new RadioButton[9];

    public SettingsPanel() { InitializeComponent(); }

    public void Init(MixerEngine engine)
    {
        _engine = engine;
        for (int i = 0; i < 9; i++)
        {
            var rb = new RadioButton { Style = (Style)FindResource("PosRadio"), GroupName = "osdpos", Tag = (OsdPosition)i };
            rb.Click += (s, _) => { _engine.Settings.Osd.Position = (OsdPosition)((RadioButton)s).Tag; _engine.SettingsChanged(); _engine.ShowOsdMessage("OSD POSITION", _engine.Settings.Osd.Position.ToString().ToUpper()); };
            _pos[i] = rb;
            PosGrid.Children.Add(rb);
        }
        Load();
        DataPath.Text = _engine.Store.DataDir;
    }

    void OpenData_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start("explorer.exe", $"\"{_engine.Store.DataDir}\""); } catch { }
    }

    void Export_Click(object sender, RoutedEventArgs e)
    {
        string name = _engine.CurrentPreset.Length > 0 ? _engine.CurrentPreset : "WinMixer";
        var dlg = new Microsoft.Win32.SaveFileDialog { Title = "Export preset", Filter = "WinMixer preset (*.json)|*.json", FileName = name + ".json" };
        if (dlg.ShowDialog() == true) _engine.ExportPreset(dlg.FileName);
    }

    void Load()
    {
        _loading = true;
        var s = _engine.Settings;
        StartMin.IsChecked = s.StartMinimized;
        MinTray.IsChecked = s.MinimizeToTray;
        CloseTray.IsChecked = s.CloseToTray;
        Startup.IsChecked = s.StartWithWindows;
        CurveLog.IsChecked = s.Curve == CurveType.Logarithmic;
        CurveLin.IsChecked = s.Curve == CurveType.Linear;
        FloorSlider.Value = s.DbFloor;
        FloorText.Text = $"{s.DbFloor:0} dB";
        FloorSlider.IsEnabled = s.Curve == CurveType.Logarithmic;
        OsdOn.IsChecked = s.Osd.Enabled;
        DurSlider.Value = s.Osd.DurationMs;
        DurText.Text = $"{s.Osd.DurationMs / 1000.0:0.0#} s";
        _pos[(int)s.Osd.Position].IsChecked = true;
        RefreshMonitors();
        _loading = false;
    }

    public void RefreshMonitors()
    {
        bool was = _loading;
        _loading = true;
        var items = new List<TargetItem>();
        var screens = System.Windows.Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            var sc = screens[i];
            items.Add(new TargetItem(sc.DeviceName, $"{i + 1}: {sc.Bounds.Width}×{sc.Bounds.Height}{(sc.Primary ? "  (primary)" : "")}"));
        }
        MonitorBox.ItemsSource = items;
        string cur = _engine.Settings.Osd.Monitor;
        MonitorBox.SelectedValue = items.Any(x => x.Key == cur) ? cur : items.FirstOrDefault(x => x.Label.Contains("primary"))?.Key;
        _loading = was;
    }

    void General_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var s = _engine.Settings;
        s.StartMinimized = StartMin.IsChecked == true;
        s.MinimizeToTray = MinTray.IsChecked == true;
        s.CloseToTray = CloseTray.IsChecked == true;
        s.StartWithWindows = Startup.IsChecked == true;
        _engine.SettingsChanged();
    }

    void Curve_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _engine.Settings.Curve = CurveLin.IsChecked == true ? CurveType.Linear : CurveType.Logarithmic;
        FloorSlider.IsEnabled = _engine.Settings.Curve == CurveType.Logarithmic;
        _engine.SettingsChanged();
    }

    void Floor_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading || _engine == null) return;
        _engine.Settings.DbFloor = e.NewValue;
        FloorText.Text = $"{e.NewValue:0} dB";
        _engine.SettingsChanged();
    }

    void Osd_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _engine.Settings.Osd.Enabled = OsdOn.IsChecked == true;
        _engine.SettingsChanged();
    }

    void Monitor_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _engine == null || MonitorBox.SelectedValue is not string dev) return;
        _engine.Settings.Osd.Monitor = dev;
        _engine.SettingsChanged();
        _engine.ShowOsdMessage("OSD SCREEN", MonitorBox.Text);
    }

    void Dur_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading || _engine == null) return;
        _engine.Settings.Osd.DurationMs = (int)e.NewValue;
        DurText.Text = $"{e.NewValue / 1000.0:0.0#} s";
        _engine.SettingsChanged();
    }

    void Test_Click(object sender, RoutedEventArgs e)
    {
        var first = _engine.Strips.FirstOrDefault();
        if (first != null) _engine.ShowOsd(first);
        else _engine.ShowOsdMessage("WINMIXER", "OSD test");
    }
}
