using System.Text.Json;

namespace SonicShielding.Windows;

internal sealed class ShieldSettings
{
    private const int CurrentSettingsSchemaVersion = 2;
    public static readonly int[] DefaultComfortEqReductions = [0, 0, 0, 0, 97, 98, 99, 100, 100];

    public int SettingsSchemaVersion { get; set; } = CurrentSettingsSchemaVersion;
    public bool Enabled { get; set; } = true;
    public string ProtectionStrength { get; set; } = "Strong";
    public int Sensitivity { get; set; } = 95;
    public int MaximumReduction { get; set; } = 99;
    public int MinimumFrequency { get; set; } = 1000;
    public int ReleaseMilliseconds { get; set; } = 110;
    public bool PreserveSpeech { get; set; } = true;
    public bool AggressiveAlarmBlocking { get; set; } = true;
    public int SuddenSoundReduction { get; set; } = 50;
    public bool ComfortEqEnabled { get; set; }
    public int[] ComfortEqReductions { get; set; } = [.. DefaultComfortEqReductions];
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
            if (!document.RootElement.TryGetProperty(nameof(SettingsSchemaVersion), out _) ||
                loaded.SettingsSchemaVersion < CurrentSettingsSchemaVersion ||
                !document.RootElement.TryGetProperty(nameof(ProtectionStrength), out _))
            {
                loaded.SettingsSchemaVersion = CurrentSettingsSchemaVersion;
                loaded.ProtectionStrength = "Strong";
                loaded.Sensitivity = 95;
                loaded.MaximumReduction = 99;
            }
            if (loaded.ComfortEqReductions is not { Length: 9 }) loaded.ComfortEqReductions = [.. DefaultComfortEqReductions];
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
