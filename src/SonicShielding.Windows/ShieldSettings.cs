using System.Text.Json;

namespace SonicShielding.Windows;

internal sealed class ShieldSettings
{
    public bool Enabled { get; set; } = true;
    public string ProtectionStrength { get; set; } = "Strong";
    public int Sensitivity { get; set; } = 95;
    public int MaximumReduction { get; set; } = 99;
    public int MinimumFrequency { get; set; } = 1000;
    public int ReleaseMilliseconds { get; set; } = 110;
    public bool PreserveSpeech { get; set; } = true;
    public bool AggressiveAlarmBlocking { get; set; } = true;
    public int SuddenSoundReduction { get; set; } = 50;
    public int[] EqualizerLevels { get; set; } = new int[7];
    public bool StartWithWindows { get; set; }

    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SonicShielding");
    private static readonly string FileName = Path.Combine(Folder, "settings.json");

    public static ShieldSettings Load()
    {
        try
        {
            var json = File.ReadAllText(FileName);
            var loaded = JsonSerializer.Deserialize<ShieldSettings>(json) ?? new();
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(nameof(AggressiveAlarmBlocking), out _)) loaded.AggressiveAlarmBlocking = true;
            if (loaded.EqualizerLevels is not { Length: 7 }) loaded.EqualizerLevels = new int[7];
            return loaded;
        }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FileName, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
