using System.Text.Json.Serialization;

namespace WinMixer.Core;

public enum MidiKind { ControlChange, Note, PitchBend }
public enum CurveType { Linear, Logarithmic }
public enum OsdPosition { TopLeft, TopCenter, TopRight, MiddleLeft, Center, MiddleRight, BottomLeft, BottomCenter, BottomRight }

public class MidiBinding
{
    public string Device { get; set; } = "";
    public MidiKind Kind { get; set; }
    public int Channel { get; set; }
    public int Number { get; set; }

    public bool Matches(MidiMessage m) =>
        m.Device == Device && m.Kind == Kind && m.Channel == Channel && (Kind == MidiKind.PitchBend || m.Number == Number);

    public string Describe(string? alias = null)
    {
        string what = Kind switch
        {
            MidiKind.ControlChange => $"CC {Number}",
            MidiKind.Note => $"NOTE {Number}",
            _ => "PITCH"
        };
        return $"{(string.IsNullOrWhiteSpace(alias) ? Device : alias)} · {what} · CH {Channel + 1}";
    }
}

public class StripConfig
{
    public string Target { get; set; } = "";
    public MidiBinding? VolumeBinding { get; set; }
    public MidiBinding? MuteBinding { get; set; }
    public bool Relative { get; set; }
    public double Position { get; set; } = 1.0;
    public bool Muted { get; set; }
}

public class ControllerConfig
{
    public string Name { get; set; } = "";
    public string Alias { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public class OsdSettings
{
    public bool Enabled { get; set; } = true;
    public string Monitor { get; set; } = "";   // Screen.DeviceName, "" = primary
    public OsdPosition Position { get; set; } = OsdPosition.BottomCenter;
    public int DurationMs { get; set; } = 1500;
}

public class AppSettings
{
    public bool StartMinimized { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public CurveType Curve { get; set; } = CurveType.Logarithmic;
    public double DbFloor { get; set; } = -60;
    public List<ControllerConfig> Controllers { get; set; } = new();
    public OsdSettings Osd { get; set; } = new();
    public string LastPreset { get; set; } = "";
    public List<StripConfig> Strips { get; set; } = new();
}

public class Preset
{
    public string Name { get; set; } = "";
    public List<StripConfig> Strips { get; set; } = new();
}

public record MidiMessage(string Device, MidiKind Kind, int Channel, int Number, int Value)
{
    /// <summary>0..1</summary>
    [JsonIgnore]
    public double Normalized => Kind == MidiKind.PitchBend ? Value / 16383.0 : Value / 127.0;
}

public static class VolumeCurve
{
    public static double ToGain(double x, CurveType curve, double floorDb)
    {
        x = Math.Clamp(x, 0, 1);
        if (curve == CurveType.Linear) return x;
        if (x < 0.005) return 0;
        return Math.Pow(10, floorDb * (1 - x) / 20.0);
    }

    public static double FromGain(double g, CurveType curve, double floorDb)
    {
        g = Math.Clamp(g, 0, 1);
        if (curve == CurveType.Linear) return g;
        if (g <= 0.0000001) return 0;
        double db = 20 * Math.Log10(g);
        return Math.Clamp(1 + db / -floorDb, 0, 1);
    }

    public static string DbText(double gain) =>
        gain <= 0.00001 ? "-∞ dB" : $"{20 * Math.Log10(gain):0.0} dB";
}
