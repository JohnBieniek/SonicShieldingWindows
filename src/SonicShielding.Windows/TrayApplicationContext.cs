using Microsoft.Win32;

namespace SonicShielding.Windows;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ShieldSettings settings = ShieldSettings.Load();
    private readonly SystemAudioShield shield;
    private readonly NotifyIcon tray;
    private readonly Icon onIcon;
    private readonly Icon offIcon;
    private SettingsForm? window;

    public TrayApplicationContext()
    {
        onIcon = IconFactory.FromResource("icon-128.png");
        offIcon = IconFactory.FromResource("icon-inactive-128.png");
        shield = new(settings);
        tray = new NotifyIcon
        {
            Visible = true,
            Text = "Sonic Shielding",
            ContextMenuStrip = BuildMenu()
        };
        shield.StatusChanged += text => tray.Text = text.Length > 63 ? text[..63] : text;
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) Toggle(); };
        tray.DoubleClick += (_, _) => ShowSettings();
        ApplyState();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Turn on / off", null, (_, _) => Toggle());
        menu.Items.Add("Comfort profile", null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    private void Toggle() { settings.Enabled = !settings.Enabled; settings.Save(); ApplyState(); window?.RefreshState(); }
    private void ApplyState()
    {
        tray.Icon = settings.Enabled ? onIcon : offIcon;
        if (settings.Enabled) shield.Start(); else shield.Stop();
    }

    private void ShowSettings()
    {
        if (window is { IsDisposed: false }) { window.Show(); window.Activate(); return; }
        window = new SettingsForm(settings, () => { settings.Save(); ApplyStartup(); ApplyState(); });
        window.Show();
    }

    private void ApplyStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (settings.StartWithWindows) key?.SetValue("SonicShielding", $"\"{Environment.ProcessPath}\""); else key?.DeleteValue("SonicShielding", false);
    }

    protected override void ExitThreadCore()
    {
        tray.Visible = false; tray.Dispose(); shield.Dispose(); onIcon.Dispose(); offIcon.Dispose(); base.ExitThreadCore();
    }
}

internal static class IconFactory
{
    public static Icon FromResource(string fileName)
    {
        var assembly = typeof(IconFactory).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var source = new Bitmap(stream);
        using var bitmap = new Bitmap(source, 64, 64);
        return Icon.FromHandle(bitmap.GetHicon()).Clone() as Icon ?? SystemIcons.Application;
    }
}
