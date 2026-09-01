param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\{[0-9a-fA-F-]{36}\}$')]
    [string]$EndpointId
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'This installer must be run as Administrator.'
}

$apoId = '{4DF8E93B-E1F7-4FC9-87D1-39F086704ECD}'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sourceDll = Join-Path $projectRoot 'native\SonicShielding.Apo\SonicShieldingApo.dll'
$installDir = Join-Path $env:ProgramFiles 'Sonic Shielding'
$installedDll = Join-Path $installDir 'SonicShieldingApo.dll'
$endpointKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\$EndpointId"
$fxKey = Join-Path $endpointKey 'FxProperties'
$backup = Join-Path $projectRoot "release\SonicShielding-endpoint-$($EndpointId.Trim('{}')).reg"

if (-not (Test-Path -LiteralPath $sourceDll)) { throw "APO build not found: $sourceDll" }
if (-not (Test-Path -LiteralPath $fxKey)) { throw "Playback endpoint not found: $EndpointId" }

& reg.exe export "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\$EndpointId" $backup /y
if ($LASTEXITCODE -ne 0) { throw 'Could not back up the playback endpoint registry configuration.' }

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item -LiteralPath $sourceDll -Destination $installedDll -Force

$classKey = "HKLM:\SOFTWARE\Classes\CLSID\$apoId\InprocServer32"
New-Item -Path $classKey -Force | Out-Null
Set-Item -Path $classKey -Value $installedDll
New-ItemProperty -Path $classKey -Name ThreadingModel -Value Both -PropertyType String -Force | Out-Null

$apoKey = "HKLM:\SOFTWARE\Classes\AudioEngine\AudioProcessingObjects\$apoId"
New-Item -Path $apoKey -Force | Out-Null
$stringValues = @{
    FriendlyName = 'Sonic Shielding frequency filter'
    Copyright = 'Copyright Sonic Shielding contributors'
    APOInterface0 = '{FD7F2B29-24D0-4B5C-B177-592C39F9CA10}'
}
$dwordValues = @{
    MajorVersion = 1; MinorVersion = 0; Flags = 13
    MinInputConnections = 1; MaxInputConnections = 1
    MinOutputConnections = 1; MaxOutputConnections = 1
    MaxInstances = [uint32]::MaxValue; NumAPOInterfaces = 1
}
foreach ($entry in $stringValues.GetEnumerator()) {
    New-ItemProperty -Path $apoKey -Name $entry.Key -Value $entry.Value -PropertyType String -Force | Out-Null
}
foreach ($entry in $dwordValues.GetEnumerator()) {
    New-ItemProperty -Path $apoKey -Name $entry.Key -Value $entry.Value -PropertyType DWord -Force | Out-Null
}

$legacyStreamEffect = (Get-ItemProperty -LiteralPath $fxKey -Name '{D04E05A6-594B-4FB6-A80D-01AF5EED7D1D},5' -ErrorAction SilentlyContinue).'{D04E05A6-594B-4FB6-A80D-01AF5EED7D1D},5'
$effects = @($legacyStreamEffect, $apoId) | Where-Object { $_ } | Select-Object -Unique
New-ItemProperty -LiteralPath $fxKey -Name '{D04E05A6-594B-4FB6-A80D-01AF5EED7D1D},13' -Value $effects -PropertyType MultiString -Force | Out-Null

$profileDir = Join-Path $env:ProgramData 'SonicShielding'
New-Item -ItemType Directory -Path $profileDir -Force | Out-Null
$acl = Get-Acl -LiteralPath $profileDir
$rule = [Security.AccessControl.FileSystemAccessRule]::new('Users', 'Modify', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
$acl.SetAccessRule($rule)
Set-Acl -LiteralPath $profileDir -AclObject $acl

Write-Output "Installed Sonic Shielding APO for $EndpointId"
Write-Output "Endpoint backup: $backup"
Write-Output 'Restart Windows to rebuild the audio graph.'
