# Steps 5-8 helper: publish team ZIP, customer sim, list handover files.
# Usage: powershell -ExecutionPolicy Bypass -File tools/build-delivery-pack.ps1

param(
    [string]$PostgresPassword,
    [switch]$SkipPublish,
    [switch]$SkipCustomerSim
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$devSettings = Join-Path $repoRoot "Pattern.Web/appsettings.Development.json"

if (-not $PostgresPassword -and (Test-Path $devSettings)) {
    $dev = Get-Content $devSettings -Raw | ConvertFrom-Json
    $conn = [string]$dev.ConnectionStrings.Postgres
    if ($conn -match 'Password=([^;]+)') { $PostgresPassword = $Matches[1] }
}
if (-not $PostgresPassword) {
    throw "Pass -PostgresPassword or set ConnectionStrings:Postgres in appsettings.Development.json"
}

if (-not $SkipPublish) {
    & (Join-Path $repoRoot "tools/publish-team-package.ps1") -PostgresPassword $PostgresPassword -SkipLaunchTest
}

$zip = Join-Path $repoRoot "dist/PatternPro-Desktop-1.0-win-x64.zip"
if (-not (Test-Path $zip)) { throw "Missing $zip" }

if (-not $SkipCustomerSim) {
    & (Join-Path $repoRoot "tools/qa-customer-sim-test.ps1") -SkipPublish -PostgresPassword $PostgresPassword
}

Write-Host ""
Write-Host "=== Delivery pack (send these) ===" -ForegroundColor Green
Write-Host "  ZIP:     $zip"
Write-Host "  Secrets: dist/delivery-credentials-*.txt (separate channel, per user)"
Write-Host "  Docs:    docs/CUSTOMER_HANDOVER.md, docs/AFTER_DELIVERY.md"
Write-Host "  Inside ZIP: HANDOVER.txt, MULTI_PC_SETUP.md, VERSION.txt"
Write-Host ""
Write-Host "Git tag when ready:" -ForegroundColor Cyan
Write-Host "  git tag -a desktop-1.0 -m ""PatternPro Desktop 1.0 team package"""
Write-Host ""
