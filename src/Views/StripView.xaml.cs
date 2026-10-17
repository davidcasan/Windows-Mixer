using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinMixer.Core;

namespace WinMixer.Views;

public partial class StripView : UserControl
{
    const string DragFormat = "WinMixerStrip";
    Strip? _strip;
    Point _dragStart;

    void Grip_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed || _strip == null) return;
        var d = e.GetPosition(null) - _dragStart;
        if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        Opacity = 0.55;
        DragDrop.DoDragDrop(this, new DataObject(DragFormat, _strip), DragDropEffects.Move);
        Opacity = 1;
    }

    void OnDragOver(object sender, DragEventArgs e)
    {
        bool ok = e.Data.GetDataPresent(DragFormat) && e.Data.GetData(DragFormat) != _strip;
        e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
        DropMark.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    void OnDrop(object sender, DragEventArgs e)
    {
        DropMark.Visibility = Visibility.Collapsed;
        if (_strip != null && e.Data.GetData(DragFormat) is Strip dragged && dragged != _strip)
            App.Engine.MoveStrip(dragged, _strip);
        e.Handled = true;
    }

    public StripView()
    {
        InitializeComponent();
        TargetBox.ItemsSource = App.Engine.Targets;
        AllowDrop = true;
        Grip.MouseLeftButtonDown += (_, e) => _dragStart = e.GetPosition(null);
        Grip.MouseMove += Grip_MouseMove;
        DragOver += OnDragOver;
        DragLeave += (_, _) => DropMark.Visibility = Visibility.Collapsed;
        Drop += OnDrop;
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
