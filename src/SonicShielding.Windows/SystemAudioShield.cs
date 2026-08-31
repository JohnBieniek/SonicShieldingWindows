using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using System.Runtime.InteropServices;

namespace SonicShielding.Windows;

internal sealed class SystemAudioShield : IDisposable
{
    private readonly object sync = new();
    private readonly ShieldSettings settings;
    private WasapiLoopbackCapture? capture;
    private MMDevice? device;
    private readonly List<float> samples = new(4096);
    private System.Threading.Timer? restoreTimer;
    private float volumeBeforeDuck;
    private bool ducked;
    private double previousRms;
    public event Action<string>? StatusChanged;

    public SystemAudioShield(ShieldSettings settings) => this.settings = settings;

    public void Start()
    {
        lock (sync)
        {
            if (capture != null) return;
            var enumerator = new MMDeviceEnumerator();
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            capture = new WasapiLoopbackCapture(device);
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
            if (samples.Count >= 1024) { Analyze(CollectionsMarshal.AsSpan(samples)[..1024], format.SampleRate); samples.RemoveRange(0, 256); }
        }
    }

    private void Analyze(ReadOnlySpan<float> block, int sampleRate)
    {
        double sum = 0;
        var fft = new Complex[1024];
        for (int i = 0; i < 1024; i++)
        {
            var value = block[i]; sum += value * value;
            fft[i].X = value * (float)FastFourierTransform.HammingWindow(i, 1024);
        }
        double rms = Math.Sqrt(sum / 1024);
        FastFourierTransform.FFT(true, 10, fft);
        int minBin = Math.Max(2, settings.MinimumFrequency * 1024 / sampleRate);
        int maxBin = Math.Min(510, 16000 * 1024 / sampleRate);
        int peaks = 0, highPeaks = 0; double bestProminence = 0;
        double needed = 12 + (50 - settings.Sensitivity) * .08 + (settings.PreserveSpeech ? 2 : 0);
        for (int bin = minBin; bin <= maxBin; bin++)
        {
            double magnitude = Magnitude(fft[bin]);
            if (magnitude <= Magnitude(fft[bin - 1]) || magnitude < Magnitude(fft[bin + 1])) continue;
            double baseline = 0; int count = 0;
            for (int j = -8; j <= 8; j++) if (Math.Abs(j) > 1) { baseline += Magnitude(fft[bin + j]); count++; }
            double prominence = 20 * Math.Log10(Math.Max(magnitude, 1e-12) / Math.Max(baseline / count, 1e-12));
            if (prominence >= needed) { peaks++; bestProminence = Math.Max(bestProminence, prominence); if (bin * sampleRate / 1024 >= 5000) highPeaks++; }
        }
        bool tone = rms >= .006 && (bestProminence >= 18 || peaks >= 2);
        bool alarm = settings.AggressiveAlarmBlocking && peaks >= (settings.PreserveSpeech ? 3 : 2) && highPeaks >= (settings.PreserveSpeech ? 2 : 1);
        bool spike = previousRms > .001 && rms > previousRms * 3.5 && rms > .12;
        previousRms = rms;
        if (tone || alarm || spike) Duck(spike ? settings.SuddenSoundReduction : settings.MaximumReduction, alarm ? 180 : settings.ReleaseMilliseconds);
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
