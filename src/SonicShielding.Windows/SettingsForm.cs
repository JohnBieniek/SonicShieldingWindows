using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SonicShielding.Windows;

internal sealed class SettingsForm : Form
{
    private static readonly (string Name, int Frequency)[] EqualizerBands = { ("Bass", 80), ("Warmth", 200), ("Body", 500), ("Presence", 1000), ("Clarity", 2500), ("Detail", 6000), ("Air", 12000) };
    private readonly ShieldSettings settings;
    private readonly Action save;
    private readonly Button stateButton;
    private readonly FlowLayoutPanel content;
    private readonly Panel viewport;
    private readonly Color navy = Color.FromArgb(7, 27, 53);
    private readonly Color card = Color.FromArgb(13, 41, 71);
    private readonly Color mint = Color.FromArgb(54, 215, 202);

    public SettingsForm(ShieldSettings settings, Action save)
    {
        this.settings = settings; this.save = save;
        Text = "Sonic Shielding — Comfort Profile"; StartPosition = FormStartPosition.CenterScreen;
        Size = new(920, 860); MinimumSize = new(640, 640); BackColor = navy; ForeColor = Color.FromArgb(238, 252, 255); Font = new("Segoe UI", 10);
        viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = navy };
        content = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Padding = new(24, 28, 24, 36), BackColor = navy };
        viewport.Controls.Add(content); Controls.Add(viewport);

        content.Controls.Add(Label("Sonic Shielding", 24, true));
        content.Controls.Add(Label("Softer sound where you need it.", 11, false, Color.FromArgb(167, 194, 209)));
        stateButton = Button(settings.Enabled ? "ON — protecting all Windows audio" : "OFF — click to turn on");
        stateButton.Click += (_, _) => { settings.Enabled = !settings.Enabled; save(); RefreshState(); };
        content.Controls.Add(Card(stateButton));
        content.Controls.Add(Card(Label("Use care with sound. This is a comfort tool, not a medical or hearing test. Begin low and stop immediately if uncomfortable.", 10, true)));

        var beep = Stack("Beep blocker", "Detects electronic beeps, dings, squeals, alarms, and sudden spikes in the complete Windows audio mix.");
        beep.Controls.Add(Combo("Protection strength", new[] { "Low", "Balanced", "Strong" }, settings.ProtectionStrength, v => { settings.ProtectionStrength = v; (settings.Sensitivity, settings.MaximumReduction) = v switch { "Low" => (35, 88), "Balanced" => (50, 94), _ => (95, 99) }; }));
        beep.Controls.Add(Slider("Tone detection sensitivity", settings.Sensitivity, 0, 100, v => settings.Sensitivity = v));
        beep.Controls.Add(Slider("Maximum tone reduction", settings.MaximumReduction, 0, 100, v => settings.MaximumReduction = v));
        beep.Controls.Add(Slider("Lowest protected frequency", settings.MinimumFrequency, 1000, 5000, v => settings.MinimumFrequency = v));
        beep.Controls.Add(Slider("Release duration", settings.ReleaseMilliseconds, 40, 250, v => settings.ReleaseMilliseconds = v));
        beep.Controls.Add(Check("Preserve speech", settings.PreserveSpeech, v => settings.PreserveSpeech = v));
        beep.Controls.Add(Check("Alarm Ambush", settings.AggressiveAlarmBlocking, v => settings.AggressiveAlarmBlocking = v));
        beep.Controls.Add(Label("Catches repeating alarm patterns more aggressively. Enabled by default.", 9, false, Color.FromArgb(167, 194, 209)));
        beep.Controls.Add(Slider("Sudden sound reduction", settings.SuddenSoundReduction, 0, 90, v => settings.SuddenSoundReduction = v));
        content.Controls.Add(Card(beep));

        var eq = Stack("Comfort equalizer", "Preview each range and shape a comfortable listening profile. Tone tests play briefly at a gentle level.");
        for (var i = 0; i < EqualizerBands.Length; i++) eq.Controls.Add(EqualizerBand(i));
        content.Controls.Add(Card(eq));
        content.Controls.Add(Card(Label("How Windows protection works", 16, true), Label("This version safely analyzes the system-wide WASAPI mix and briefly lowers the active output endpoint only when a qualifying tone or spike is detected. It does not record, upload, or save audio. Equalizer controls are a saved comfort profile and tone-preview tool; applying frequency-only EQ across Windows requires a signed audio-effect driver.", 10)));
        content.Controls.Add(Card(Check("Start Sonic Shielding with Windows", settings.StartWithWindows, v => settings.StartWithWindows = v)));
        var saveButton = Button("Save comfort profile"); saveButton.Click += (_, _) => { save(); saveButton.Text = "Saved"; }; content.Controls.Add(saveButton);
        content.Controls.Add(Label("Everything stays on your device.", 10, true, Color.FromArgb(167, 194, 209)));
        content.Controls.Add(WhimsyLogo());

        viewport.Resize += (_, _) => LayoutContent(); Shown += (_, _) => LayoutContent();
        FormClosing += (_, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
        RefreshState();
    }

    public void RefreshState() { stateButton.Text = settings.Enabled ? "ON — protecting all Windows audio" : "OFF — click to turn on"; stateButton.BackColor = settings.Enabled ? mint : Color.FromArgb(225, 74, 67); }

    private void LayoutContent()
    {
        var width = Math.Min(860, Math.Max(540, viewport.ClientSize.Width - 36));
        content.Width = width; content.Left = Math.Max(0, (viewport.ClientSize.Width - width) / 2);
        ResizeChildren(content, width - content.Padding.Horizontal - 8);
    }

    private static void ResizeChildren(Control parent, int width)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is PictureBox picture) { picture.Width = Math.Min(300, width); continue; }
            child.Width = width;
            if (child is Label label)
                label.Height = TextRenderer.MeasureText(label.Text, label.Font, new Size(Math.Max(100, width - 8), 0), TextFormatFlags.WordBreak).Height + 10;
            if (child is Panel or FlowLayoutPanel) ResizeChildren(child, Math.Max(440, width - child.Padding.Horizontal));
        }
    }

    private Control EqualizerBand(int index)
    {
        var band = EqualizerBands[index];
        var row = new TableLayoutPanel { Height = 62, ColumnCount = 3, Margin = new(4, 3, 4, 3) };
        row.ColumnStyles.Add(new(SizeType.Percent, 28)); row.ColumnStyles.Add(new(SizeType.Percent, 52)); row.ColumnStyles.Add(new(SizeType.Percent, 20));
        row.Controls.Add(Label($"{band.Name}\n{FormatFrequency(band.Frequency)}", 9, true), 0, 0);
        var slider = new TrackBar { Minimum = -12, Maximum = 12, Value = Math.Clamp(settings.EqualizerLevels[index], -12, 12), TickFrequency = 3, Dock = DockStyle.Fill };
        slider.ValueChanged += (_, _) => settings.EqualizerLevels[index] = slider.Value; row.Controls.Add(slider, 1, 0);
        var test = Button("Test tone"); test.Height = 36; test.Dock = DockStyle.Top; test.Click += async (_, _) => await PlayToneAsync(band.Frequency, test); row.Controls.Add(test, 2, 0);
        return row;
    }

    private static async Task PlayToneAsync(int frequency, Button button)
    {
        button.Enabled = false; button.Text = "Playing…";
        try
        {
            using var output = new WaveOutEvent { DesiredLatency = 100 };
            var tone = new SignalGenerator(44100, 1) { Gain = 0.06, Frequency = frequency, Type = SignalGeneratorType.Sin };
            output.Init(tone.ToWaveProvider()); output.Play(); await Task.Delay(650); output.Stop();
        }
        catch (Exception ex) { MessageBox.Show($"The tone could not be played.\n\n{ex.Message}", "Tone test", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        finally { button.Enabled = true; button.Text = "Test tone"; }
    }

    private PictureBox WhimsyLogo()
    {
        var assembly = typeof(SettingsForm).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("whimsy-logo.png", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name)!; using var source = Image.FromStream(stream);
        return new PictureBox { Image = new Bitmap(source), Height = 118, SizeMode = PictureBoxSizeMode.Zoom, Margin = new(4, 8, 4, 8) };
    }

    private static string FormatFrequency(int frequency) => frequency >= 1000 ? $"{frequency / 1000d:0.#} kHz" : $"{frequency} Hz";
    private Label Label(string text, float size, bool bold = false, Color? color = null) { var font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular); return new Label { Text = text, AutoSize = false, Height = TextRenderer.MeasureText(text, font, new Size(780, 0), TextFormatFlags.WordBreak).Height + 10, ForeColor = color ?? ForeColor, Font = font, Margin = new(4, 5, 4, 8) }; }
    private Button Button(string text) => new() { Text = text, Height = 44, FlatStyle = FlatStyle.Flat, BackColor = mint, ForeColor = Color.FromArgb(5, 32, 43), Font = new("Segoe UI", 10, FontStyle.Bold), Margin = new(4, 8, 4, 8) };
    private Panel Card(params Control[] controls) { var p = Stack("", ""); p.BackColor = card; p.Padding = new(16); p.Margin = new(4, 10, 4, 10); foreach (var c in controls) p.Controls.Add(c); return p; }
    private FlowLayoutPanel Stack(string title, string description) { var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true }; if (title.Length > 0) p.Controls.Add(Label(title, 16, true)); if (description.Length > 0) p.Controls.Add(Label(description, 10, false, Color.FromArgb(167, 194, 209))); return p; }
    private Control Slider(string name, int value, int min, int max, Action<int> changed) { var p = new TableLayoutPanel { Height = 58, ColumnCount = 2 }; p.ColumnStyles.Add(new(SizeType.Percent, 40)); p.ColumnStyles.Add(new(SizeType.Percent, 60)); p.Controls.Add(Label(name, 9, true), 0, 0); var s = new TrackBar { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), TickStyle = TickStyle.None, Dock = DockStyle.Fill }; s.ValueChanged += (_, _) => changed(s.Value); p.Controls.Add(s, 1, 0); return p; }
    private Control Check(string name, bool value, Action<bool> changed) { var c = new CheckBox { Text = name, Checked = value, AutoSize = false, Height = 34, ForeColor = ForeColor, Margin = new(5, 9, 5, 2) }; c.CheckedChanged += (_, _) => changed(c.Checked); return c; }
    private Control Combo(string name, string[] values, string value, Action<string> changed) { var p = new TableLayoutPanel { Height = 48, ColumnCount = 2 }; p.ColumnStyles.Add(new(SizeType.Percent, 40)); p.ColumnStyles.Add(new(SizeType.Percent, 60)); p.Controls.Add(Label(name, 9, true), 0, 0); var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill }; c.Items.AddRange(values); c.SelectedItem = value; c.SelectedValueChanged += (_, _) => changed(c.SelectedItem?.ToString() ?? "Strong"); p.Controls.Add(c, 1, 0); return p; }
}
