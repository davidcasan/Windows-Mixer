using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinMixer.Core;

namespace WinMixer.Views;

public partial class StripView : UserControl
{
    Strip? _strip;

    public StripView()
    {
        InitializeComponent();
        TargetBox.ItemsSource = App.Engine.Targets;
        DataContextChanged += (_, e) =>
        {
            if (_strip != null) _strip.PropertyChanged -= OnChanged;
            _strip = e.NewValue as Strip;
            if (_strip != null) _strip.PropertyChanged += OnChanged;
            UpdateVisuals();
        };
        Unloaded += (_, _) => { if (_strip != null) _strip.PropertyChanged -= OnChanged; };
        Loaded += (_, _) => { if (_strip != null) { _strip.PropertyChanged -= OnChanged; _strip.PropertyChanged += OnChanged; } UpdateVisuals(); };
    }

    void OnChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Strip.Online) or nameof(Strip.LearnVolume) or nameof(Strip.LearnMute) or nameof(Strip.Target))
            UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (_strip == null) return;
        Link.Fill = new SolidColorBrush(_strip.Online ? Color.FromRgb(0x3C, 0xE0, 0x5A) : Color.FromRgb(0x3A, 0x14, 0x10));
        Link.Effect = _strip.Online ? new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x3C, 0xE0, 0x5A), ShadowDepth = 0, BlurRadius = 8 } : null;
        LearnVol.Content = _strip.LearnVolume ? "● LEARNING" : "LEARN";
        LearnMute.Content = _strip.LearnMute ? "● LEARNING" : "LEARN";
        LearnVol.Foreground = _strip.LearnVolume ? (Brush)FindResource("Amber") : (Brush)FindResource("TextBrush");
        LearnMute.Foreground = _strip.LearnMute ? (Brush)FindResource("Amber") : (Brush)FindResource("TextBrush");
    }

    void Remove_Click(object sender, RoutedEventArgs e) => _strip?.Remove();
    void LearnVol_Click(object sender, RoutedEventArgs e) => _strip?.ToggleLearn(false);
    void LearnMute_Click(object sender, RoutedEventArgs e) => _strip?.ToggleLearn(true);
    void ClearVol_Click(object sender, RoutedEventArgs e) => _strip?.ClearVolumeBinding();
    void ClearMute_Click(object sender, RoutedEventArgs e) => _strip?.ClearMuteBinding();
}
