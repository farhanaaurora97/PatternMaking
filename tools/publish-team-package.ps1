# One-command team package for other PCs: extract ZIP, double-click exe, login.
# Usage (main PC only):  powershell -ExecutionPolicy Bypass -File tools/publish-team-package.ps1

param(
    [string]$ServerHost,
    [int]$PostgresPort = 5433,
    [string]$PostgresPassword = "1234",
    [string]$PostgresDatabase = "patternpro",
    [switch]$SkipFirewall,
    [switch]$SkipLaunchTest
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not $ServerHost) {
    $ServerHost = & (Join-Path $PSScriptRoot "allow-lan-postgres.ps1") -ShowIpOnly
    if (-not $ServerHost) {
        throw "Could not detect LAN IP. Run: ipconfig and pass -ServerHost 192.168.x.x"
    }
}

Write-Host ""
Write-Host "=== PatternPro TEAM package (zero setup for other PCs) ===" -ForegroundColor Cyan
Write-Host "Server IP: $ServerHost`:$PostgresPort" -ForegroundColor Yellow
Write-Host ""

if (-not $SkipFirewall) {
    & (Join-Path $PSScriptRoot "allow-lan-postgres.ps1") -Port $PostgresPort
}

& (Join-Path $PSScriptRoot "publish-desktop-windows.ps1") `
    -Configuration Release `
    -TeamServerHost $ServerHost `
    -PostgresPort $PostgresPort `
    -PostgresPassword $PostgresPassword `
    -PostgresDatabase $PostgresDatabase `
    -SkipMsix `
    $(if ($SkipLaunchTest) { "-SkipLaunchTest" })

Write-Host ""
Write-Host "Share this ZIP with designers (USB / Drive / network share):" -ForegroundColor Green
Write-Host "  $repoRoot\dist\PatternPro-Desktop-1.0-win-x64.zip"
Write-Host ""
Write-Host "They only need to:" -ForegroundColor Cyan
Write-Host "  1. Extract ZIP"
Write-Host "  2. Double-click PatternPro.Desktop.exe (or START-PatternPro.bat)"
Write-Host "  3. Login: admin / Admin@123"
Write-Host ""
