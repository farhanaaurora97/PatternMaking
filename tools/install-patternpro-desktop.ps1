# Install PatternPro Desktop from a published folder (creates shortcuts).
# Run from the folder that contains PatternPro.Desktop.exe, or pass -SourceDir.

param(
    [string]$SourceDir = $PSScriptRoot
)

$ErrorActionPreference = "Stop"
$exe = Join-Path $SourceDir "PatternPro.Desktop.exe"
if (-not (Test-Path $exe)) {
    throw "PatternPro.Desktop.exe not found in: $SourceDir`nRun tools/publish-desktop-windows.ps1 first, or cd to the published folder."
}

$installRoot = Join-Path $env:LOCALAPPDATA "Programs\PatternPro"
Write-Host "Installing PatternPro Desktop to $installRoot ..."

if (Test-Path $installRoot) {
    Remove-Item -Recurse -Force $installRoot
}
New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
Copy-Item -Path (Join-Path $SourceDir "*") -Destination $installRoot -Recurse -Force

$wsh = New-Object -ComObject WScript.Shell
$desktopLink = Join-Path ([Environment]::GetFolderPath("Desktop")) "PatternPro.lnk"
$startLink = Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs\PatternPro.lnk"

foreach ($linkPath in @($desktopLink, $startLink)) {
    $linkDir = Split-Path $linkPath -Parent
    if (-not (Test-Path $linkDir)) { New-Item -ItemType Directory -Force -Path $linkDir | Out-Null }
    $shortcut = $wsh.CreateShortcut($linkPath)
    $shortcut.TargetPath = Join-Path $installRoot "PatternPro.Desktop.exe"
    $shortcut.WorkingDirectory = $installRoot
    $shortcut.Description = "PatternPro Desktop"
    $shortcut.Save()
}

Write-Host ""
Write-Host "Installed successfully." -ForegroundColor Green
Write-Host "  Start menu: PatternPro"
Write-Host "  Desktop:    PatternPro shortcut"
Write-Host "  Folder:     $installRoot"
Write-Host ""
