using Microsoft.Win32;

namespace SonicShielding.Windows;

/// <summary>
/// Controls the native system-effects APO. Audio processing happens inside
/// the Windows audio engine; this class never captures audio or changes the
/// endpoint/master volume.
/// </summary>
internal sealed class SystemAudioShield : IDisposable
{
    private const string ApoClsid = "{4DF8E93B-E1F7-4FC9-87D1-39F086704ECD}";
    private readonly ShieldSettings settings;

    public event Action<string>? StatusChanged;

    public SystemAudioShield(ShieldSettings settings) => this.settings = settings;

    public void Start()
    {
        if (!IsApoRegistered())
            throw new InvalidOperationException("The Sonic Shielding audio filter is not installed on this output device.");

        WriteProfile();
        StatusChanged?.Invoke("Filtering high frequencies in the Windows audio engine");
    }

    public void Stop()
    {
        WriteProfile(enabled: false);
        StatusChanged?.Invoke("Protection is off");
    }

    private static bool IsApoRegistered()
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Classes\CLSID\{ApoClsid}\InprocServer32");
        return key?.GetValue(null) is string path && File.Exists(path);
    }

    private void WriteProfile(bool? enabled = null)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SonicShielding");
        Directory.CreateDirectory(folder);
        var values = settings.ComfortEqReductions is { Length: 9 }
            ? settings.ComfortEqReductions
            : ShieldSettings.DefaultComfortEqReductions;
        File.WriteAllLines(Path.Combine(folder, "profile.txt"),
        [
            (enabled ?? settings.Enabled).ToString(),
            string.Join(',', values.Select(value => Math.Clamp(value, 0, 100)))
        ]);
    }

    public void Dispose() { }
}
