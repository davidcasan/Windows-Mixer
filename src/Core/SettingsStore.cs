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
        string beside = Path.Combine(exeDir, "data");
        if (CanWrite(beside))
        {
            DataDir = beside;
            return;
        }

        // Exe lives somewhere read-only (e.g. Program Files): keep the data in AppData,
        // seeding it once from the "data" folder shipped next to the exe.
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinMixer");
        Directory.CreateDirectory(Path.Combine(dir, "presets"));
        DataDir = dir;
        SeedFrom(beside, dir);
    }

    static bool CanWrite(string dir)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "presets"));
            string probe = Path.Combine(dir, ".w");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }

    static void SeedFrom(string source, string target)
    {
        try
        {
            string marker = Path.Combine(target, ".seeded");
            if (File.Exists(marker) || !Directory.Exists(source)) return;
            string cfg = Path.Combine(source, "settings.json");
            if (File.Exists(cfg)) File.Copy(cfg, Path.Combine(target, "settings.json"), true);
            string presets = Path.Combine(source, "presets");
            if (Directory.Exists(presets))
                foreach (var f in Directory.GetFiles(presets, "*.json"))
                    File.Copy(f, Path.Combine(target, "presets", Path.GetFileName(f)), true);
            File.WriteAllText(marker, "");
        }
        catch { }
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

    public Preset? ReadPresetFile(string path)
    {
        try { return JsonSerializer.Deserialize<Preset>(File.ReadAllText(path), Json); }
        catch { return null; }
    }

    public void WritePresetFile(string path, Preset p) =>
        File.WriteAllText(path, JsonSerializer.Serialize(p, Json));

    public bool PresetExists(string name) => File.Exists(Path.Combine(PresetDir, Safe(name) + ".json"));
}
