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
    public bool AggressiveAlarmBlocking { get; set; }
    public int SuddenSoundReduction { get; set; } = 50;
    public bool StartWithWindows { get; set; }

    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SonicShielding");
    private static readonly string FileName = Path.Combine(Folder, "settings.json");

    public static ShieldSettings Load()
    {
        try { return JsonSerializer.Deserialize<ShieldSettings>(File.ReadAllText(FileName)) ?? new(); }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FileName, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
