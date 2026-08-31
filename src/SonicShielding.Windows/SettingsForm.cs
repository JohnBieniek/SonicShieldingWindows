namespace SonicShielding.Windows;

internal sealed class SettingsForm : Form
{
    private readonly ShieldSettings settings;
    private readonly Action save;
    private readonly Button stateButton;
    private readonly Color navy = Color.FromArgb(7, 27, 53);
    private readonly Color card = Color.FromArgb(13, 41, 71);
    private readonly Color mint = Color.FromArgb(54, 215, 202);

    public SettingsForm(ShieldSettings settings, Action save)
    {
        this.settings = settings; this.save = save;
        Text = "Sonic Shielding — Comfort profile"; Size = new(760, 800); MinimumSize = new(620, 650);
        BackColor = navy; ForeColor = Color.FromArgb(238, 252, 255); Font = new("Segoe UI", 10); AutoScroll = true;
        var content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new(24), BackColor = navy };
        Controls.Add(content);
        content.SizeChanged += (_, _) => { foreach (Control c in content.Controls) c.Width = Math.Max(520, content.ClientSize.Width - 54); };
        content.Controls.Add(Label("Sonic Shielding", 22, true));
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
        beep.Controls.Add(Check("Aggressive alarm blocking", settings.AggressiveAlarmBlocking, v => settings.AggressiveAlarmBlocking = v));
        beep.Controls.Add(Slider("Sudden sound reduction", settings.SuddenSoundReduction, 0, 90, v => settings.SuddenSoundReduction = v));
        content.Controls.Add(Card(beep));
        content.Controls.Add(Card(Label("How Windows protection works", 16, true), Label("This version safely analyzes the system-wide WASAPI mix and briefly lowers the active output endpoint only when a qualifying tone or spike is detected. It does not record, upload, or save audio. Frequency-only notching and permanent comfort EQ require a separately signed Windows audio-effect driver and are not claimed by this build.", 10)));
        content.Controls.Add(Card(Check("Start Sonic Shielding with Windows", settings.StartWithWindows, v => settings.StartWithWindows = v)));
        var saveButton = Button("Save comfort profile"); saveButton.Click += (_, _) => { save(); saveButton.Text = "Saved"; };
        content.Controls.Add(saveButton);
        content.Controls.Add(Label("Everything stays on your device.\n\nCreated in partnership with Whimsy.", 10, true, Color.FromArgb(167, 194, 209)));
        FormClosing += (_, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
    }

    public void RefreshState() { stateButton.Text = settings.Enabled ? "ON — protecting all Windows audio" : "OFF — click to turn on"; stateButton.BackColor = settings.Enabled ? mint : Color.FromArgb(225, 74, 67); }
    private Label Label(string text, float size, bool bold = false, Color? color = null) => new() { Text = text, AutoSize = true, MaximumSize = new(650, 0), ForeColor = color ?? ForeColor, Font = new("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new(4, 5, 4, 8) };
    private Button Button(string text) => new() { Text = text, Height = 44, Width = 650, FlatStyle = FlatStyle.Flat, BackColor = mint, ForeColor = Color.FromArgb(5, 32, 43), Font = new("Segoe UI", 10, FontStyle.Bold), Margin = new(4, 8, 4, 8) };
    private Panel Card(params Control[] controls) { var p = Stack("", ""); p.BackColor = card; p.Padding = new(16); p.Margin = new(4, 10, 4, 10); foreach (var c in controls) p.Controls.Add(c); p.Height = controls.Sum(x => x.Height + x.Margin.Vertical) + 36; return p; }
    private FlowLayoutPanel Stack(string title, string description) { var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Width = 650 }; if (title.Length > 0) p.Controls.Add(Label(title, 16, true)); if (description.Length > 0) p.Controls.Add(Label(description, 10, false, Color.FromArgb(167, 194, 209))); return p; }
    private Control Slider(string name, int value, int min, int max, Action<int> changed) { var p = new TableLayoutPanel { Width = 610, Height = 58, ColumnCount = 2 }; p.ColumnStyles.Add(new(SizeType.Percent, 42)); p.ColumnStyles.Add(new(SizeType.Percent, 58)); p.Controls.Add(Label(name, 9, true), 0, 0); var s = new TrackBar { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), TickStyle = TickStyle.None, Dock = DockStyle.Fill }; s.ValueChanged += (_, _) => changed(s.Value); p.Controls.Add(s, 1, 0); return p; }
    private Control Check(string name, bool value, Action<bool> changed) { var c = new CheckBox { Text = name, Checked = value, AutoSize = true, ForeColor = ForeColor, Margin = new(5, 9, 5, 9) }; c.CheckedChanged += (_, _) => changed(c.Checked); return c; }
    private Control Combo(string name, string[] values, string value, Action<string> changed) { var p = new TableLayoutPanel { Width = 610, Height = 48, ColumnCount = 2 }; p.ColumnStyles.Add(new(SizeType.Percent, 42)); p.ColumnStyles.Add(new(SizeType.Percent, 58)); p.Controls.Add(Label(name, 9, true), 0, 0); var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill }; c.Items.AddRange(values); c.SelectedItem = value; c.SelectedValueChanged += (_, _) => changed(c.SelectedItem?.ToString() ?? "Strong"); p.Controls.Add(c, 1, 0); return p; }
}
