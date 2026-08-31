#define MyAppName "Sonic Shielding for Windows"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "John Bieniek"
#define MyAppExeName "Sonic Shielding for Windows.exe"

[Setup]
AppId={{B33BF171-F184-49DD-90B7-395473D31B55}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\Sonic Shielding for Windows
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=SonicShieldingWindows-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Sonic Shielding for Windows"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Start Sonic Shielding for Windows"; Flags: nowait postinstall skipifsilent
