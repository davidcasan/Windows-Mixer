using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace WinMixer.Core;

/// <summary>Per-application volume via Windows Core Audio sessions.</summary>
public sealed class AudioService : IDisposable
{
    public const string Master = "@master";
    public const string Active = "@active";

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

    readonly MMDeviceEnumerator _enum = new();
    MMDevice? _device;
    string? _deviceId;
    readonly Dictionary<string, List<AudioSessionControl>> _sessions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Process name of the app that currently has focus (ignoring WinMixer itself).</summary>
    public string ActiveApp { get; private set; } = "";
    uint _lastPid;

    /// <returns>true when the focused app changed.</returns>
    public bool UpdateActive()
    {
        try
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
            if (pid == 0 || pid == Environment.ProcessId || pid == _lastPid) return false;
            _lastPid = pid;
            string name = Process.GetProcessById((int)pid).ProcessName;
            if (string.Equals(name, ActiveApp, StringComparison.OrdinalIgnoreCase)) return false;
            ActiveApp = name;
            return true;
        }
        catch { return false; }
    }

    string Resolve(string target) => target == Active ? ActiveApp : target;

    public IEnumerable<string> RunningApps => _sessions.Keys;

    public void Refresh()
    {
        try
        {
            using var dev = _enum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (_device == null || _deviceId != dev.ID)
            {
                _device?.Dispose();
                _device = _enum.GetDevice(dev.ID);
                _deviceId = dev.ID;
            }
            DisposeSessions();
            var mgr = _device.AudioSessionManager;
            mgr.RefreshSessions();
            var col = mgr.Sessions;
            for (int i = 0; i < col.Count; i++)
            {
                var s = col[i];
                string? name = NameOf(s);
                if (name == null) { s.Dispose(); continue; }
                if (!_sessions.TryGetValue(name, out var list)) _sessions[name] = list = new();
                list.Add(s);
            }
        }
        catch { }
    }

    static string? NameOf(AudioSessionControl s)
    {
        try
        {
            if (s.State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateExpired) return null;
            if (s.IsSystemSoundsSession) return "System Sounds";
            uint pid = s.GetProcessID;
            if (pid == 0) return null;
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch { return null; }
    }

    void DisposeSessions()
    {
        foreach (var l in _sessions.Values) foreach (var s in l) { try { s.Dispose(); } catch { } }
        _sessions.Clear();
    }

    List<AudioSessionControl>? Sessions(string target)
    {
        string t = Resolve(target);
        return t.Length > 0 && _sessions.TryGetValue(t, out var l) ? l : null;
    }

    public bool Has(string target) =>
        target == Master ? _device != null : Sessions(target) is { Count: > 0 };

    public bool SetVolume(string target, float gain)
    {
        try
        {
            gain = Math.Clamp(gain, 0f, 1f);
            if (target == Master) { if (_device == null) return false; _device.AudioEndpointVolume.MasterVolumeLevelScalar = gain; return true; }
            var l = Sessions(target); if (l == null) return false;
            foreach (var s in l) s.SimpleAudioVolume.Volume = gain;
            return true;
        }
        catch { return false; }
    }

    public bool SetMute(string target, bool mute)
    {
        try
        {
            if (target == Master) { if (_device == null) return false; _device.AudioEndpointVolume.Mute = mute; return true; }
            var l = Sessions(target); if (l == null) return false;
            foreach (var s in l) s.SimpleAudioVolume.Mute = mute;
            return true;
        }
        catch { return false; }
    }

    public float? GetVolume(string target)
    {
        try
        {
            if (target == Master) return _device?.AudioEndpointVolume.MasterVolumeLevelScalar;
            return Sessions(target)?.FirstOrDefault()?.SimpleAudioVolume.Volume;
        }
        catch { return null; }
    }

    public bool? GetMute(string target)
    {
        try
        {
            if (target == Master) return _device?.AudioEndpointVolume.Mute;
            return Sessions(target)?.FirstOrDefault()?.SimpleAudioVolume.Mute;
        }
        catch { return null; }
    }

    public (float l, float r)? GetPeaks(string target)
    {
        try
        {
            if (target == Master)
            {
                if (_device == null) return null;
                return Read(_device.AudioMeterInformation);
            }
            var list = Sessions(target); if (list == null) return null;
            float l = 0, r = 0;
            foreach (var s in list)
            {
                var (a, b) = Read(s.AudioMeterInformation);
                l = Math.Max(l, a); r = Math.Max(r, b);
            }
            return (l, r);
        }
        catch { return null; }
    }

    static (float, float) Read(AudioMeterInformation m)
    {
        var p = m.PeakValues;
        if (p.Count >= 2) return (p[0], p[1]);
        float v = m.MasterPeakValue;
        return (v, v);
    }

    public void Dispose()
    {
        DisposeSessions();
        _device?.Dispose();
        _enum.Dispose();
    }
}
