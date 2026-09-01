# Sonic Shielding for Windows

Sonic Shielding sits in the Windows notification area and watches the complete audio mix from the active output device. Click the tray icon to turn protection on or off; double-click it to open the comfort profile.

## What this build does

- Analyzes audio locally through Windows WASAPI loopback. No audio is recorded, stored, or uploaded.
- Detects prominent high-frequency tones and sudden sound spikes across browsers, media players, games, calls, and other applications using the default Windows output.
- Briefly attenuates the system output when a qualifying sound is present, then restores the previous volume.
- Preserves saved protection choices and can start with Windows.
- Includes a seven-band comfort profile with a gentle tone-test button for every band.
- Uses the original Sonic Shielding active and inactive icons and visual language.

## Driver version

The `driver-version` branch replaces master-volume attenuation with a native Windows Audio Processing Object (APO). Its real-time DSP processes 32-bit float PCM inside the Windows audio engine and applies frequency-selective cuts without changing endpoint volume. The native frequency-response test verifies that the default profile preserves low and speech-range content while strongly reducing the configured upper bands.

The tray/controller can be shipped as a portable executable, but Windows must install and register the APO before system-wide filtering is possible. That installation boundary is required by the Windows audio engine and is what allows the filter to cover browsers, games, protected applications, and other processes without injecting code into them. Development builds require test signing; public builds require a Microsoft-accepted signed driver package.

## Install

For most people, download `SonicShieldingWindows-Setup.exe` from Releases and follow the installer. It installs per-user, does not require administrator access, and can be removed from Windows Settings.

For a portable copy, download `Sonic Shielding for Windows.exe`. No installation is required.

Windows SmartScreen may warn about new unsigned applications. Public trust requires signing the installer and executable with an organization-validated or extended-validation Authenticode certificate. The release workflow supports a future signing step; do not bypass SmartScreen for a file obtained from anywhere other than this repository.

## Build

Requires the .NET 8 SDK on Windows:

```powershell
dotnet restore
dotnet test -c Release
dotnet publish src/SonicShielding.Windows/SonicShielding.Windows.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true
```

## Privacy and safety

See [PRIVACY.md](PRIVACY.md). Sonic Shielding is a comfort tool, not medical care, hearing protection, or an emergency alert system. Do not rely on it to make unsafe listening levels safe.
