# Clean PatternPro Desktop build output, WebView2 cache, and optional dist folder.
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools/clean-desktop.ps1
#   powershell -ExecutionPolicy Bypass -File tools/clean-desktop.ps1 -IncludeDist

param(
    [switch]$IncludeDist
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$desktop = Join-Path $repoRoot "PatternPro.Desktop"

Write-Host ""
Write-Host "Cleaning PatternPro Desktop..." -ForegroundColor Cyan

Get-Process -Name PatternPro.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

foreach ($dir in @("bin", "obj"))
{
    $path = Join-Path $desktop $dir
    if (Test-Path $path)
    {
        Remove-Item $path -Recurse -Force
        Write-Host "  Removed $dir/" -ForegroundColor Green
    }
}

$wv = Join-Path $desktop "bin"
Get-ChildItem -Path $wv -Recurse -Directory -Filter "EBWebView" -ErrorAction SilentlyContinue |
    ForEach-Object {
        Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  Removed WebView2 cache $($_.FullName)" -ForegroundColor Green
    }

if ($IncludeDist)
{
    $dist = Join-Path $repoRoot "dist"
    if (Test-Path $dist)
    {
        Remove-Item $dist -Recurse -Force
        Write-Host "  Removed dist/" -ForegroundColor Green
    }
}

Push-Location $repoRoot
dotnet clean PatternPro.Desktop/PatternPro.Desktop.csproj -v q | Out-Null
Pop-Location

Write-Host ""
Write-Host "Desktop clean complete. Rebuild with:" -ForegroundColor Cyan
Write-Host "  dotnet build PatternPro.Desktop/PatternPro.Desktop.csproj -c Release" -ForegroundColor DarkGray
Write-Host "  powershell -ExecutionPolicy Bypass -File tools/qa-desktop-test.ps1 -LaunchOnly" -ForegroundColor DarkGray
Write-Host ""
