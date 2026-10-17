using System.Threading;
using System.Windows;
using WinMixer.Core;
using WinMixer.Views;

namespace WinMixer;

public partial class App : Application
{
    public static MixerEngine Engine { get; private set; } = null!;
    public static bool Quitting { get; private set; }

    Mutex? _mutex;
    EventWaitHandle? _showEvent;
    MainWindow? _main;
    OsdWindow? _osd;
    System.Windows.Forms.NotifyIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "WinMixer_SingleInstance_" + Environment.UserName, out bool first);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "WinMixer_Show_" + Environment.UserName);
        if (!first)
        {
            _showEvent.Set();
            Shutdown();
            return;
        }
        new Thread(() =>
        {
            while (_showEvent.WaitOne()) Dispatcher.BeginInvoke(ShowMain);
        }) { IsBackground = true }.Start();

        Engine = new MixerEngine();
        _main = new MainWindow(Engine);
        _osd = new OsdWindow(Engine);
        Engine.OsdRequested += i => _osd.Display(i);
        Engine.PresetChanged += RebuildTrayMenu;

        BuildTray();

        bool minimized = e.Args.Contains("--minimized") || Engine.Settings.StartMinimized;
        if (!minimized) ShowMain();
    }

    void ShowMain()
    {
        if (_main == null) return;
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    void BuildTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            Text = "WinMixer",
            Visible = true
        };
        _tray.MouseClick += (_, ev) =>
        {
            if (ev.Button != System.Windows.Forms.MouseButtons.Left) return;
            if (_main!.IsVisible) _main.Hide(); else ShowMain();
        };
        RebuildTrayMenu();
    }

    void RebuildTrayMenu()
    {
        if (_tray == null) return;
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open WinMixer", null, (_, _) => ShowMain());
        var presets = new System.Windows.Forms.ToolStripMenuItem("Presets");
        foreach (var p in Engine.Presets)
        {
            string name = p;
            var item = new System.Windows.Forms.ToolStripMenuItem(name) { Checked = name == Engine.CurrentPreset };
            item.Click += (_, _) => Engine.LoadPreset(name);
            presets.DropDownItems.Add(item);
        }
        if (presets.DropDownItems.Count == 0) presets.DropDownItems.Add(new System.Windows.Forms.ToolStripMenuItem("(no presets)") { Enabled = false });
        menu.Items.Add(presets);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Quit());
        _tray.ContextMenuStrip = menu;
    }

    public void Quit()
    {
        Quitting = true;
        _tray?.Dispose();
        _tray = null;
        Engine?.Dispose();
        _osd?.Close();
        _main?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
