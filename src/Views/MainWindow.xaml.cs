using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinMixer.Core;

namespace WinMixer.Views;

public partial class MainWindow : Window
{
    readonly MixerEngine _engine;
    bool _updatingPresets;

    public MainWindow(MixerEngine engine)
    {
        _engine = engine;
        InitializeComponent();

        StripList.ItemsSource = engine.Strips;
        PresetBox.ItemsSource = engine.Presets;
        CtrlPanel.Init(engine);
        SetPanel.Init(engine);
        SyncPreset();

        engine.PresetChanged += () => Dispatcher.BeginInvoke(SyncPreset);
        engine.MidiActivity += t => MidiLog.Text = t;

        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Maximized)
                Root.Margin = new Thickness(SystemParameters.WindowResizeBorderThickness.Left + 2);
            else Root.Margin = new Thickness(0);
            if (WindowState == WindowState.Minimized && engine.Settings.MinimizeToTray)
            {
                Hide();
                WindowState = WindowState.Normal;
            }
        };
        Closing += OnClosing;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { _engine.CancelLearn(); Prompt.Visibility = Visibility.Collapsed; } };
    }

    void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (App.Quitting) return;
        if (_engine.Settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            e.Cancel = true;
            ((App)Application.Current).Quit();
        }
    }

    void SyncPreset()
    {
        _updatingPresets = true;
        PresetBox.SelectedItem = _engine.Presets.Contains(_engine.CurrentPreset) ? _engine.CurrentPreset : null;
        PresetHint.Visibility = PresetBox.SelectedItem == null ? Visibility.Visible : Visibility.Collapsed;
        _updatingPresets = false;
    }

    void PresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPresets) return;
        if (PresetBox.SelectedItem is string name) _engine.LoadPreset(name);
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_engine.CurrentPreset.Length > 0) _engine.SavePreset(_engine.CurrentPreset);
        else SaveAs_Click(sender, e);
    }

    void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        PromptBox.Text = _engine.CurrentPreset;
        Prompt.Visibility = Visibility.Visible;
        PromptBox.Focus();
        PromptBox.SelectAll();
    }

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        string name = _engine.CurrentPreset;
        if (name.Length == 0) return;
        if (MessageBox.Show(this, $"Delete preset \"{name}\"?", "WinMixer", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            _engine.DeletePreset(name);
    }

    void PromptOk_Click(object sender, RoutedEventArgs e)
    {
        string name = PromptBox.Text.Trim();
        if (name.Length == 0) return;
        Prompt.Visibility = Visibility.Collapsed;
        _engine.SavePreset(name);
    }

    void PromptCancel_Click(object sender, RoutedEventArgs e) => Prompt.Visibility = Visibility.Collapsed;

    void PromptBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) PromptOk_Click(sender, e);
    }

    void Panel_Changed(object sender, RoutedEventArgs e)
    {
        if (sender == CtrlBtn && CtrlBtn.IsChecked == true) SetBtn.IsChecked = false;
        else if (sender == SetBtn && SetBtn.IsChecked == true) CtrlBtn.IsChecked = false;
        CtrlPanel.Visibility = CtrlBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SetPanel.Visibility = SetBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (SetBtn.IsChecked == true) SetPanel.RefreshMonitors();
    }

    void Add_Click(object sender, RoutedEventArgs e)
    {
        _engine.AddStrip();
        Dispatcher.BeginInvoke(() => Rack.ScrollToRightEnd(), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    void Min_Click(object sender, RoutedEventArgs e)
    {
        if (_engine.Settings.MinimizeToTray) Hide();
        else WindowState = WindowState.Minimized;
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();
}
