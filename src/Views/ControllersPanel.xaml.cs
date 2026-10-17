using System.Windows;
using System.Windows.Controls;
using WinMixer.Core;

namespace WinMixer.Views;

public partial class ControllersPanel : UserControl
{
    public ControllersPanel() { InitializeComponent(); }

    public void Init(MixerEngine engine)
    {
        List.ItemsSource = engine.Controllers;
        void upd() => Empty.Visibility = engine.Controllers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        engine.Controllers.CollectionChanged += (_, _) => upd();
        upd();
    }

    void Register_Click(object sender, RoutedEventArgs e) => (((FrameworkElement)sender).Tag as ControllerRow)?.Register();
    void Remove_Click(object sender, RoutedEventArgs e) => (((FrameworkElement)sender).Tag as ControllerRow)?.Remove();
}
