using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinMixer.Core;

/// <summary>Portable storage: everything lives in a "data" folder next to the exe.</summary>
public sealed class SettingsStore
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string DataDir { get; }
    string PresetDir => Path.Combine(DataDir, "presets");
    string SettingsPath => Path.Combine(DataDir, "settings.json");

    public SettingsStore()
    {
        string exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        string dir = Path.Combine(exeDir, "data");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "presets"));
            string probe = Path.Combine(dir, ".w");
            File.WriteAllText(probe, "");
            File.Delete(probe);
        }
        catch
        {
            dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinMixer");
            Directory.CreateDirectory(Path.Combine(dir, "presets"));
        }
        DataDir = dir;
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), Json) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public void Save(AppSettings s)
    {
        try
        {
            string tmp = SettingsPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(s, Json));
            File.Move(tmp, SettingsPath, true);
        }
        catch { }
    }

    static string Safe(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Trim();
    }

    public List<string> ListPresets() =>
        Directory.Exists(PresetDir)
            ? Directory.GetFiles(PresetDir, "*.json").Select(f => Path.GetFileNameWithoutExtension(f)).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList()
            : new();

    public void SavePreset(Preset p)
    {
        Directory.CreateDirectory(PresetDir);
        File.WriteAllText(Path.Combine(PresetDir, Safe(p.Name) + ".json"), JsonSerializer.Serialize(p, Json));
    }

    public Preset? LoadPreset(string name)
    {
        try
        {
            string path = Path.Combine(PresetDir, Safe(name) + ".json");
            return File.Exists(path) ? JsonSerializer.Deserialize<Preset>(File.ReadAllText(path), Json) : null;
        }
        catch { return null; }
    }

    public void DeletePreset(string name)
    {
        try { File.Delete(Path.Combine(PresetDir, Safe(name) + ".json")); } catch { }
    }

    public string PresetName(string name) => Safe(name);
}
