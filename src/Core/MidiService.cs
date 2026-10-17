using NAudio.Midi;

namespace WinMixer.Core;

public sealed class MidiService : IDisposable
{
    readonly Dictionary<string, MidiIn> _open = new();
    public event Action<MidiMessage>? Message;

    public Dictionary<string, string> Status { get; } = new();

    public static List<string> ListDevices()
    {
        var l = new List<string>();
        for (int i = 0; i < MidiIn.NumberOfDevices; i++) l.Add(MidiIn.DeviceInfo(i).ProductName);
        return l;
    }

    /// <summary>Opens enabled+present controllers, closes the rest.</summary>
    public void Sync(IEnumerable<ControllerConfig> controllers, List<string> present)
    {
        var list = controllers.ToList();
        var wanted = list.Where(c => c.Enabled).Select(c => c.Name).ToHashSet();
        foreach (var name in _open.Keys.ToList())
            if (!wanted.Contains(name) || !present.Contains(name)) Close(name);

        var prev = new Dictionary<string, string>(Status);
        Status.Clear();
        foreach (var c in list)
        {
            if (!present.Contains(c.Name)) { Status[c.Name] = "DISCONNECTED"; continue; }
            if (!c.Enabled) { Status[c.Name] = "DISABLED"; continue; }
            if (_open.ContainsKey(c.Name)) { Status[c.Name] = "ACTIVE"; continue; }
            try
            {
                var mi = new MidiIn(present.IndexOf(c.Name));
                string dev = c.Name;
                mi.MessageReceived += (_, e) => Handle(dev, e.RawMessage);
                mi.ErrorReceived += (_, _) => { };
                mi.Start();
                _open[c.Name] = mi;
                Status[c.Name] = "ACTIVE";
            }
            catch { Status[c.Name] = "BUSY (in use by another app?)"; }
        }
    }

    void Close(string name)
    {
        if (!_open.Remove(name, out var mi)) return;
        try { mi.Stop(); mi.Dispose(); } catch { }
    }

    void Handle(string dev, int raw)
    {
        int status = raw & 0xFF;
        int type = status & 0xF0, ch = status & 0x0F;
        int d1 = (raw >> 8) & 0x7F, d2 = (raw >> 16) & 0x7F;
        MidiMessage? m = type switch
        {
            0xB0 => new(dev, MidiKind.ControlChange, ch, d1, d2),
            0x90 when d2 > 0 => new(dev, MidiKind.Note, ch, d1, d2),
            0xE0 => new(dev, MidiKind.PitchBend, ch, 0, d1 | (d2 << 7)),
            _ => null
        };
        if (m != null) Message?.Invoke(m);
    }

    public void Dispose()
    {
        foreach (var n in _open.Keys.ToList()) Close(n);
    }
}
