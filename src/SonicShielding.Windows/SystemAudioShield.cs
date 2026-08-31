using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using System.Runtime.InteropServices;

namespace SonicShielding.Windows;

internal sealed class SystemAudioShield : IDisposable
{
    private readonly object sync = new();
    private readonly ShieldSettings settings;
    private WasapiCapture? capture;
    private MMDevice? device;
    private readonly List<float> samples = new(4096);
    private System.Threading.Timer? restoreTimer;
    private float volumeBeforeDuck;
    private bool ducked;
    private double previousRms;
    private bool onsetArmed = true;
    public event Action<string>? StatusChanged;

    public SystemAudioShield(ShieldSettings settings) => this.settings = settings;

    public void Start()
    {
        lock (sync)
        {
            if (capture != null) return;
            var enumerator = new MMDeviceEnumerator();
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            // The stock loopback capture uses a comparatively large buffer. A short
            // event-driven buffer keeps the detector close to the sound's leading edge.
            capture = new LowLatencyLoopbackCapture(device, 10);
            capture.DataAvailable += OnAudio;
            capture.RecordingStopped += (_, e) => { if (e.Exception != null) StatusChanged?.Invoke(e.Exception.Message); };
            capture.StartRecording();
            StatusChanged?.Invoke($"Protecting all sound on {device.FriendlyName}");
        }
    }

    public void Stop()
    {
        lock (sync)
        {
            capture?.StopRecording(); capture?.Dispose(); capture = null;
            RestoreVolume();
            samples.Clear();
            previousRms = 0;
            onsetArmed = true;
            StatusChanged?.Invoke("Protection is off");
        }
    }

    private void OnAudio(object? sender, WaveInEventArgs e)
    {
        var format = capture!.WaveFormat;
        int bytes = format.BitsPerSample / 8;
        int frame = bytes * format.Channels;
        for (int offset = 0; offset + frame <= e.BytesRecorded; offset += frame)
        {
            double mono = 0;
            for (int channel = 0; channel < format.Channels; channel++)
            {
                int index = offset + channel * bytes;
                mono += format.Encoding == WaveFormatEncoding.IeeeFloat
                    ? BitConverter.ToSingle(e.Buffer, index)
                    : format.BitsPerSample == 16 ? BitConverter.ToInt16(e.Buffer, index) / 32768f : 0;
            }
            samples.Add((float)(mono / format.Channels));
            if (samples.Count >= 512) { Analyze(CollectionsMarshal.AsSpan(samples)[..512], format.SampleRate); samples.RemoveRange(0, 128); }
        }
    }

    private void Analyze(ReadOnlySpan<float> block, int sampleRate)
    {
        double sum = 0;
        var fft = new Complex[512];
        for (int i = 0; i < 512; i++)
        {
            var value = block[i]; sum += value * value;
            fft[i].X = value * (float)FastFourierTransform.HammingWindow(i, 512);
        }
        double rms = Math.Sqrt(sum / 512);
        FastFourierTransform.FFT(true, 9, fft);
        int minBin = Math.Max(9, settings.MinimumFrequency * 512 / sampleRate);
        int maxBin = Math.Min(247, 16000 * 512 / sampleRate);
        int peaks = 0, highPeaks = 0; double bestProminence = 0;
        double needed = 12 + (50 - settings.Sensitivity) * .08 + (settings.PreserveSpeech ? 2 : 0);
        for (int bin = minBin; bin <= maxBin; bin++)
        {
            double magnitude = Magnitude(fft[bin]);
            if (magnitude <= Magnitude(fft[bin - 1]) || magnitude < Magnitude(fft[bin + 1])) continue;
            double baseline = 0; int count = 0;
            for (int j = -8; j <= 8; j++) if (Math.Abs(j) > 1) { baseline += Magnitude(fft[bin + j]); count++; }
            double prominence = 20 * Math.Log10(Math.Max(magnitude, 1e-12) / Math.Max(baseline / count, 1e-12));
            if (prominence >= needed) { peaks++; bestProminence = Math.Max(bestProminence, prominence); if (bin * sampleRate / 512 >= 5000) highPeaks++; }
        }
        bool tone = rms >= .004 && (bestProminence >= needed || peaks >= 2);
        bool alarm = settings.AggressiveAlarmBlocking && peaks >= (settings.PreserveSpeech ? 3 : 2) && highPeaks >= (settings.PreserveSpeech ? 2 : 1);
        // Alarms often fade in over a few blocks, so a one-block 3.5x comparison
        // misses their attack. Arm after silence and react while the sound is still
        // quiet enough to lower the endpoint before its painful peak arrives.
        bool protectedOnset = settings.AggressiveAlarmBlocking && onsetArmed && rms >= .012;
        if (protectedOnset) onsetArmed = false;
        else if (rms < .003) onsetArmed = true;
        bool spike = previousRms > .001 && rms > previousRms * 2.2 && rms > .06;
        previousRms = rms;
        if (tone || alarm || spike || protectedOnset)
        {
            int reduction = protectedOnset ? settings.MaximumReduction : spike ? settings.SuddenSoundReduction : settings.MaximumReduction;
            int release = protectedOnset ? Math.Max(240, settings.ReleaseMilliseconds) : alarm ? 180 : settings.ReleaseMilliseconds;
            Duck(reduction, release);
        }
    }

    private static double Magnitude(Complex value) => Math.Sqrt(value.X * value.X + value.Y * value.Y);

    private void Duck(int reduction, int release)
    {
        lock (sync)
        {
            if (device == null) return;
            if (!ducked) { volumeBeforeDuck = device.AudioEndpointVolume.MasterVolumeLevelScalar; ducked = true; }
            float target = volumeBeforeDuck * Math.Max(.01f, 1 - reduction / 100f);
            if (device.AudioEndpointVolume.MasterVolumeLevelScalar > target) device.AudioEndpointVolume.MasterVolumeLevelScalar = target;
            restoreTimer?.Dispose();
            restoreTimer = new(_ => RestoreVolume(), null, release, Timeout.Infinite);
        }
    }

    private void RestoreVolume()
    {
        lock (sync)
        {
            restoreTimer?.Dispose(); restoreTimer = null;
            if (ducked && device != null) device.AudioEndpointVolume.MasterVolumeLevelScalar = volumeBeforeDuck;
            ducked = false;
        }
    }

    public void Dispose() { Stop(); restoreTimer?.Dispose(); device?.Dispose(); }
}

internal sealed class LowLatencyLoopbackCapture : WasapiCapture
{
    public LowLatencyLoopbackCapture(MMDevice device, int latencyMilliseconds)
        : base(device, true, latencyMilliseconds) { }

    protected override AudioClientStreamFlags GetAudioClientStreamFlags() => AudioClientStreamFlags.Loopback;
}
