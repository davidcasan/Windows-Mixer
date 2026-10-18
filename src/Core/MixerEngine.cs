using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WinMixer.Core;

public record OsdInfo(string Title, string Text, double? Fraction, bool Muted);

public sealed class ControllerRow : INotifyPropertyChanged
{
    readonly MixerEngine _engine;
    string _status = "";
    public ControllerRow(MixerEngine e, string name) { _engine = e; Name = name; }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Raise([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public string Name { get; }
    public ControllerConfig? Config { get; set; }
    public bool Registered => Config != null;
    public bool Present { get; set; }

    public string Alias
    {
        get => Config?.Alias ?? "";
        set { if (Config == null || Config.Alias == value) return; Config.Alias = value; Raise(); _engine.ControllersChanged(); }
    }

    public bool Enabled
    {
        get => Config?.Enabled ?? false;
        set { if (Config == null || Config.Enabled == value) return; Config.Enabled = value; Raise(); _engine.ControllersChanged(); }
    }

    public string Status { get => _status; set { if (_status == value) return; _status = value; Raise(); } }

    public void Register() => _engine.RegisterController(Name);
    public void Remove() => _engine.RemoveController(Name);
}

/// <summary>Glue between MIDI input, Windows audio sessions, the strips and the UI.</summary>
public sealed class MixerEngine : IDisposable
{
    public AudioService Audio { get; } = new();
    public MidiService Midi { get; } = new();
    public SettingsStore Store { get; } = new();
    public AppSettings Settings { get; }

    public ObservableCollection<Strip> Strips { get; } = new();
    public ObservableCollection<TargetItem> Targets { get; } = new();
    public ObservableCollection<string> Presets { get; } = new();
    public ObservableCollection<ControllerRow> Controllers { get; } = new();

    public string CurrentPreset { get; private set; } = "";

    public event Action<OsdInfo>? OsdRequested;
    public event Action<string>? MidiActivity;
    public event Action? PresetChanged;

    readonly DispatcherTimer _fast = new() { Interval = TimeSpan.FromMilliseconds(33) };
    readonly DispatcherTimer _slow = new() { Interval = TimeSpan.FromMilliseconds(1000) };
    readonly DispatcherTimer _save = new() { Interval = TimeSpan.FromMilliseconds(400) };
    readonly Dispatcher _ui = Dispatcher.CurrentDispatcher;

    (Strip strip, bool mute)? _learn;
    string _controllerSig = "";

    public MixerEngine()
    {
        Settings = Store.Load();
        CurrentPreset = Settings.LastPreset;

        if (Settings.Strips.Count == 0)
        {
            Settings.Strips.Add(new StripConfig { Target = AudioService.Master });
            Settings.Strips.Add(new StripConfig { Target = AudioService.Active });
            Settings.Strips.Add(new StripConfig());
            Settings.Strips.Add(new StripConfig());
        }
        foreach (var cfg in Settings.Strips) Strips.Add(MakeLoaded(cfg));

        foreach (var p in Store.ListPresets()) Presets.Add(p);

        Midi.Message += m => _ui.BeginInvoke(() => HandleMidi(m));

        _fast.Tick += (_, _) => FastTick();
        _slow.Tick += (_, _) => SlowTick();
        _save.Tick += (_, _) => { _save.Stop(); SaveNow(); };

        SlowTick();
        _fast.Start();
        _slow.Start();
    }

    Strip MakeLoaded(StripConfig cfg)
    {
        var s = new Strip(this, cfg);
        bool active = cfg.Target == AudioService.Active;
        s.NeedsApply = cfg.Target.Length > 0 && !active;
        s.NeedsRead = active || cfg.Target.Length == 0;
        return s;
    }

    // ---------- helpers ----------
    public double GainOf(double position) => VolumeCurve.ToGain(position, Settings.Curve, Settings.DbFloor);
    public double PositionOf(double gain) => VolumeCurve.FromGain(gain, Settings.Curve, Settings.DbFloor);

    public string AliasOf(string device) =>
        Settings.Controllers.FirstOrDefault(c => c.Name == device)?.Alias ?? "";

    public string DisplayName(Strip s) =>
        s.Target == AudioService.Master ? "MASTER"
        : s.Target == AudioService.Active ? (Audio.ActiveApp.Length > 0 ? Audio.ActiveApp : "ACTIVE WINDOW")
        : s.Target.Length == 0 ? "—" : s.Target;

    public void MarkDirty() { _save.Stop(); _save.Start(); }

    public void SaveNow()
    {
        Settings.Strips = Strips.Select(s => s.Config).ToList();
        Settings.LastPreset = CurrentPreset;
        Store.Save(Settings);
    }

    // ---------- strips ----------
    public Strip AddStrip()
    {
        var s = new Strip(this, new StripConfig());
        Strips.Add(s);
        MarkDirty();
        return s;
    }

    public void RemoveStrip(Strip s)
    {
        if (_learn?.strip == s) CancelLearn();
        Strips.Remove(s);
        MarkDirty();
    }

    /// <summary>Moves a strip to the position currently held by another one.</summary>
    public void MoveStrip(Strip s, Strip before)
    {
        int from = Strips.IndexOf(s), to = Strips.IndexOf(before);
        if (from < 0 || to < 0 || from == to) return;
        Strips.Move(from, to);
        MarkDirty();
    }

    public void StripTargetChanged(Strip s)
    {
        s.RefreshSubLabel();
        SyncTargets();
        PollStrips();
        MarkDirty();
    }

    public void StripVolumeChanged(Strip s)
    {
        ApplyVolume(s);
        MarkDirty();
    }

    public void StripMuteChanged(Strip s)
    {
        ApplyMute(s);
        MarkDirty();
    }

    void ApplyVolume(Strip s)
    {
        if (s.Target.Length == 0) return;
        float g = (float)GainOf(s.Position);
        if (Audio.SetVolume(s.Target, g)) { s.LastGain = g; s.NeedsApply = false; s.NeedsRead = false; }
        else s.NeedsApply = true;
    }

    void ApplyMute(Strip s)
    {
        if (s.Target.Length == 0) return;
        if (Audio.SetMute(s.Target, s.Muted)) { s.LastMute = s.Muted; s.NeedsApply = false; s.NeedsRead = false; }
        else s.NeedsApply = true;
    }

    public void SettingsChanged()
    {
        foreach (var s in Strips) { s.RefreshReadout(); ApplyVolume(s); }
        StartupService.Set(Settings.StartWithWindows);
        MarkDirty();
    }

    // ---------- timers ----------
    void FastTick()
    {
        if (Audio.UpdateActive())
        {
            foreach (var s in Strips.Where(s => s.IsActiveTarget))
            {
                s.NeedsApply = false;
                s.NeedsRead = true;
                s.RefreshSubLabel();
            }
            PollStrips();
        }

        foreach (var s in Strips)
        {
            double l = 0, r = 0;
            if (s.Online && Audio.GetPeaks(s.Target) is { } p)
            {
                l = ToMeter(p.l);
                r = ToMeter(p.r);
            }
            Smooth(s, true, l);
            Smooth(s, false, r);
        }
    }

    static double ToMeter(float peak) =>
        peak <= 0.00001f ? 0 : Math.Clamp((20 * Math.Log10(peak) + 48) / 48, 0, 1);

    static void Smooth(Strip s, bool left, double target)
    {
        double cur = left ? s.LevelL : s.LevelR;
        double next = target >= cur ? target : Math.Max(target, cur - 0.035);
        if (Math.Abs(next - cur) < 0.001) return;
        if (left) s.LevelL = next; else s.LevelR = next;
    }

    void SlowTick()
    {
        Audio.Refresh();

        var present = MidiService.ListDevices();
        Midi.Sync(Settings.Controllers, present);
        SyncControllerRows(present);

        SyncTargets();
        PollStrips();
    }

    void PollStrips()
    {
        foreach (var s in Strips)
        {
            bool online = s.Target.Length > 0 && Audio.Has(s.Target);
            s.Online = online;
            if (!online) continue;

            if (s.NeedsApply)
            {
                ApplyVolume(s);
                ApplyMute(s);
                continue;
            }

            if (Audio.GetVolume(s.Target) is { } g && (s.NeedsRead || Math.Abs(g - s.LastGain) > 0.01))
            {
                s.LastGain = g;
                s.SetSilently(position: PositionOf(g));
            }
            if (Audio.GetMute(s.Target) is { } m && (s.NeedsRead || m != s.LastMute))
            {
                s.LastMute = m;
                s.SetSilently(muted: m);
            }
            s.NeedsRead = false;
        }
    }

    void SyncTargets()
    {
        var wanted = new List<TargetItem>
        {
            new(AudioService.Master, "MASTER"),
            new(AudioService.Active, "ACTIVE WINDOW"),
        };
        var running = Audio.RunningApps.OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var a in running) wanted.Add(new(a, a));
        foreach (var t in Strips.Select(s => s.Target).Where(t => t.Length > 0).Distinct())
            if (wanted.All(w => w.Key != t)) wanted.Add(new(t, t + " (offline)"));

        for (int i = Targets.Count - 1; i >= 0; i--)
            if (wanted.All(w => w.Key != Targets[i].Key)) Targets.RemoveAt(i);
        foreach (var w in wanted)
        {
            int idx = Targets.ToList().FindIndex(t => t.Key == w.Key);
            if (idx < 0) Targets.Add(w);
            else if (Targets[idx] != w) Targets[idx] = w;
        }
    }

    // ---------- MIDI ----------
    void HandleMidi(MidiMessage m)
    {
        MidiActivity?.Invoke($"{AliasOrName(m.Device)}  ·  {Describe(m)}  ·  {m.Value}");

        if (_learn is { } l)
        {
            TryLearn(l.strip, l.mute, m);
            return;
        }

        foreach (var s in Strips.ToList())
        {
            var vb = s.Config.VolumeBinding;
            if (vb != null && vb.Matches(m) && m.Kind != MidiKind.Note)
            {
                if (s.Relative && m.Kind == MidiKind.ControlChange)
                {
                    int d = m.Value < 64 ? m.Value : m.Value - 128;
                    s.Position += d * 0.015;
                }
                else s.Position = m.Normalized;
                ShowOsd(s);
            }

            var mb = s.Config.MuteBinding;
            if (mb != null && mb.Matches(m))
            {
                bool press = m.Kind == MidiKind.Note || (m.Kind == MidiKind.ControlChange && m.Value >= 64);
                if (press)
                {
                    s.Muted = !s.Muted;
                    ShowOsd(s);
                }
            }
        }
    }

    string AliasOrName(string dev) => AliasOf(dev) is { Length: > 0 } a ? a : dev;

    static string Describe(MidiMessage m) => m.Kind switch
    {
        MidiKind.ControlChange => $"CC {m.Number} CH {m.Channel + 1}",
        MidiKind.Note => $"NOTE {m.Number} CH {m.Channel + 1}",
        _ => $"PITCH CH {m.Channel + 1}"
    };

    public void ShowOsd(Strip s)
    {
        string text = s.Muted ? "MUTED" : $"{s.ReadoutDb}   ·   {s.ReadoutPct.Replace("SYS ", "")}";
        OsdRequested?.Invoke(new OsdInfo(DisplayName(s), text, s.Position, s.Muted));
    }

    public void ShowOsdMessage(string title, string text) =>
        OsdRequested?.Invoke(new OsdInfo(title, text, null, false));

    // ---------- learn ----------
    public void ToggleLearn(Strip s, bool mute)
    {
        bool same = _learn is { } l && l.strip == s && l.mute == mute;
        CancelLearn();
        if (same) return;
        _learn = (s, mute);
        if (mute) s.LearnMute = true; else s.LearnVolume = true;
    }

    public void CancelLearn()
    {
        if (_learn is { } l) { l.strip.LearnVolume = false; l.strip.LearnMute = false; }
        _learn = null;
    }

    void TryLearn(Strip s, bool mute, MidiMessage m)
    {
        bool ok = mute
            ? m.Kind == MidiKind.Note || (m.Kind == MidiKind.ControlChange && m.Value >= 64)
            : m.Kind != MidiKind.Note;
        if (!ok) return;

        var b = new MidiBinding { Device = m.Device, Kind = m.Kind, Channel = m.Channel, Number = m.Number };
        if (mute) s.Config.MuteBinding = b; else s.Config.VolumeBinding = b;
        CancelLearn();
        s.RefreshBindingText();
        MarkDirty();
    }

    // ---------- controllers ----------
    public void RegisterController(string name)
    {
        if (Settings.Controllers.Any(c => c.Name == name)) return;
        Settings.Controllers.Add(new ControllerConfig { Name = name });
        ControllersChanged();
    }

    public void RemoveController(string name)
    {
        Settings.Controllers.RemoveAll(c => c.Name == name);
        ControllersChanged();
    }

    public void ControllersChanged()
    {
        _controllerSig = "";
        SlowTick();
        foreach (var s in Strips) s.RefreshBindingText();
        MarkDirty();
    }

    void SyncControllerRows(List<string> present)
    {
        var names = Settings.Controllers.Select(c => c.Name).Union(present).Distinct().ToList();
        string sig = string.Join("|", names.Select(n =>
            $"{n}:{Settings.Controllers.Any(c => c.Name == n)}:{present.Contains(n)}"));
        if (sig != _controllerSig)
        {
            _controllerSig = sig;
            Controllers.Clear();
            foreach (var n in names)
                Controllers.Add(new ControllerRow(this, n)
                {
                    Config = Settings.Controllers.FirstOrDefault(c => c.Name == n),
                    Present = present.Contains(n)
                });
        }
        foreach (var r in Controllers)
            r.Status = r.Registered ? Midi.Status.GetValueOrDefault(r.Name, "") : "NOT REGISTERED";
    }

    // ---------- presets ----------
    public void SavePreset(string name)
    {
        name = Store.PresetName(name);
        if (name.Length == 0) return;
        SaveNow();
        Store.SavePreset(new Preset { Name = name, Strips = Strips.Select(s => s.Config).ToList() });
        if (!Presets.Contains(name))
        {
            int i = 0;
            while (i < Presets.Count && string.Compare(Presets[i], name, StringComparison.CurrentCultureIgnoreCase) < 0) i++;
            Presets.Insert(i, name);
        }
        CurrentPreset = name;
        MarkDirty();
        PresetChanged?.Invoke();
        ShowOsdMessage("PRESET SAVED", name);
    }

    public void LoadPreset(string name)
    {
        var p = Store.LoadPreset(name);
        if (p == null) return;
        CancelLearn();
        Strips.Clear();
        foreach (var cfg in p.Strips) Strips.Add(MakeLoaded(cfg));
        CurrentPreset = name;
        SyncTargets();
        PollStrips();
        MarkDirty();
        PresetChanged?.Invoke();
        ShowOsdMessage("PRESET", name);
    }

    /// <summary>Reads a preset .json from anywhere on disk, adds it to the preset list and loads it.</summary>
    public bool ImportPreset(string path)
    {
        var p = Store.ReadPresetFile(path);
        if (p == null || p.Strips == null || p.Strips.Count == 0) return false;
        string name = Store.PresetName(string.IsNullOrWhiteSpace(p.Name) ? Path.GetFileNameWithoutExtension(path) : p.Name);
        if (name.Length == 0) name = "Imported";
        string unique = name;
        for (int i = 2; Store.PresetExists(unique); i++) unique = $"{name} ({i})";
        p.Name = unique;
        Store.SavePreset(p);
        if (!Presets.Contains(unique))
        {
            int idx = 0;
            while (idx < Presets.Count && string.Compare(Presets[idx], unique, StringComparison.CurrentCultureIgnoreCase) < 0) idx++;
            Presets.Insert(idx, unique);
        }
        LoadPreset(unique);
        return true;
    }

    public void ExportPreset(string path)
    {
        SaveNow();
        var name = CurrentPreset.Length > 0 ? CurrentPreset : "WinMixer";
        Store.WritePresetFile(path, new Preset { Name = name, Strips = Strips.Select(s => s.Config).ToList() });
    }

    public void DeletePreset(string name)
    {
        Store.DeletePreset(name);
        Presets.Remove(name);
        if (CurrentPreset == name) CurrentPreset = "";
        MarkDirty();
        PresetChanged?.Invoke();
    }

    public void Dispose()
    {
        _fast.Stop(); _slow.Stop(); _save.Stop();
        SaveNow();
        Midi.Dispose();
        Audio.Dispose();
    }
}

public static class StartupService
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "WinMixer";

    public static void Set(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(Key, true);
            if (k == null) return;
            if (on) k.SetValue(Name, $"\"{Environment.ProcessPath}\"");
            else k.DeleteValue(Name, false);
        }
        catch { }
    }
}
