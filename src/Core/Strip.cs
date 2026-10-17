using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinMixer.Core;

public record TargetItem(string Key, string Label)
{
    public override string ToString() => Label;
}

/// <summary>One mixer channel: a target (app / master / active window) + MIDI bindings.</summary>
public sealed class Strip : INotifyPropertyChanged
{
    readonly MixerEngine _engine;
    bool _suppress;
    bool _online;
    double _levelL, _levelR;
    bool _learnVol, _learnMute;

    public StripConfig Config { get; }

    // sync state (engine-owned)
    public bool NeedsApply;
    public bool NeedsRead = true;
    public double LastGain = -1;
    public bool LastMute;

    public Strip(MixerEngine engine, StripConfig cfg)
    {
        _engine = engine;
        Config = cfg;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Raise([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    void Raise(params string[] names) { foreach (var n in names) Raise(n); }

    public string Target
    {
        get => Config.Target;
        set
        {
            if (value == null || Config.Target == value) return;
            Config.Target = value;
            NeedsApply = false;
            NeedsRead = true;
            Raise(nameof(Target), nameof(SubLabel), nameof(IsActiveTarget));
            _engine.StripTargetChanged(this);
        }
    }

    public bool IsActiveTarget => Config.Target == AudioService.Active;

    /// <summary>Small caption under the readout: which app "Active window" currently points to.</summary>
    public string SubLabel =>
        IsActiveTarget ? (_engine.Audio.ActiveApp.Length > 0 ? "→ " + _engine.Audio.ActiveApp : "→ (none)")
        : Config.Target == AudioService.Master ? "SYSTEM OUTPUT"
        : Config.Target.Length == 0 ? "NO TARGET" : "";

    public double Position
    {
        get => Config.Position;
        set
        {
            value = Math.Clamp(value, 0, 1);
            if (Math.Abs(Config.Position - value) < 1e-9) return;
            Config.Position = value;
            Raise(nameof(Position), nameof(ReadoutDb), nameof(ReadoutPct));
            if (!_suppress) _engine.StripVolumeChanged(this);
        }
    }

    public bool Muted
    {
        get => Config.Muted;
        set
        {
            if (Config.Muted == value) return;
            Config.Muted = value;
            Raise();
            if (!_suppress) _engine.StripMuteChanged(this);
        }
    }

    public bool Relative
    {
        get => Config.Relative;
        set { if (Config.Relative == value) return; Config.Relative = value; Raise(); _engine.MarkDirty(); }
    }

    public bool Online
    {
        get => _online;
        set { if (_online == value) return; _online = value; Raise(); }
    }

    public double LevelL { get => _levelL; set { _levelL = value; Raise(); } }
    public double LevelR { get => _levelR; set { _levelR = value; Raise(); } }

    public double Gain => _engine.GainOf(Position);
    public string ReadoutDb => VolumeCurve.DbText(Gain);
    public string ReadoutPct => $"SYS {Gain * 100:0}%";

    public bool LearnVolume { get => _learnVol; set { if (_learnVol == value) return; _learnVol = value; Raise(nameof(LearnVolume), nameof(VolumeBindingText)); } }
    public bool LearnMute { get => _learnMute; set { if (_learnMute == value) return; _learnMute = value; Raise(nameof(LearnMute), nameof(MuteBindingText)); } }

    public string VolumeBindingText =>
        _learnVol ? "MOVE A KNOB / FADER…" : Config.VolumeBinding?.Describe(_engine.AliasOf(Config.VolumeBinding.Device)) ?? "— not assigned —";
    public string MuteBindingText =>
        _learnMute ? "PRESS A BUTTON…" : Config.MuteBinding?.Describe(_engine.AliasOf(Config.MuteBinding.Device)) ?? "— not assigned —";

    public bool HasVolumeBinding => Config.VolumeBinding != null;
    public bool HasMuteBinding => Config.MuteBinding != null;

    public void RefreshBindingText()
    {
        Raise(nameof(VolumeBindingText), nameof(MuteBindingText), nameof(HasVolumeBinding), nameof(HasMuteBinding));
    }

    public void RefreshReadout() => Raise(nameof(ReadoutDb), nameof(ReadoutPct));
    public void RefreshSubLabel() => Raise(nameof(SubLabel));

    /// <summary>Update from the system / engine without triggering a write back to Windows.</summary>
    public void SetSilently(double? position = null, bool? muted = null)
    {
        _suppress = true;
        try
        {
            if (position.HasValue) Position = position.Value;
            if (muted.HasValue) Muted = muted.Value;
        }
        finally { _suppress = false; }
    }

    public void ClearVolumeBinding() { Config.VolumeBinding = null; RefreshBindingText(); _engine.MarkDirty(); }
    public void ClearMuteBinding() { Config.MuteBinding = null; RefreshBindingText(); _engine.MarkDirty(); }
    public void ToggleLearn(bool mute) => _engine.ToggleLearn(this, mute);
    public void Remove() => _engine.RemoveStrip(this);
}
