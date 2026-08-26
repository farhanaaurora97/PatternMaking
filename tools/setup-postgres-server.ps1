# Configure PC 1 (PostgreSQL server) for team Desktop connections.
# Run on the PC that hosts PostgreSQL. Firewall step needs Administrator.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools/setup-postgres-server.ps1
#   powershell -ExecutionPolicy Bypass -File tools/setup-postgres-server.ps1 -Port 5433

param(
    [int]$Port = 5433
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent

Write-Host ""
Write-Host "=== PatternPro — PostgreSQL server setup (PC 1) ===" -ForegroundColor Cyan
Write-Host ""

# Local IPv4 addresses (skip loopback)
$ips = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike "127.*" -and $_.PrefixOrigin -ne "WellKnown" } |
    Select-Object -ExpandProperty IPAddress -Unique

Write-Host "This PC's IP address(es) — use one as Host on other PCs:" -ForegroundColor Yellow
if ($ips) { $ips | ForEach-Object { Write-Host "  $_" -ForegroundColor Green } }
else { Write-Host "  (could not detect — run ipconfig)" -ForegroundColor Red }

Write-Host ""
Write-Host "1) PostgreSQL config (edit manually, then restart PostgreSQL service):" -ForegroundColor Cyan
Write-Host "   postgresql.conf:"
Write-Host '     listen_addresses = *'
Write-Host "     port = $Port"
Write-Host ""
Write-Host "   pg_hba.conf (adjust subnet to your office LAN):"
Write-Host "     host    patternpro    postgres    192.168.1.0/24    scram-sha-256"
Write-Host ""

Write-Host "2) Windows Firewall — allow inbound TCP $Port ..." -ForegroundColor Cyan
$ruleName = "PatternPro PostgreSQL ($Port)"
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "   Rule already exists: $ruleName" -ForegroundColor DarkGray
}
else {
    try {
        New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -ErrorAction Stop | Out-Null
        Write-Host "   Created firewall rule: $ruleName" -ForegroundColor Green
    }
    catch {
        Write-Host "   Could not create firewall rule (run PowerShell as Administrator):" -ForegroundColor Red
        Write-Host "   New-NetFirewallRule -DisplayName `"$ruleName`" -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow"
    }
}

Write-Host ""
Write-Host "3) Test local database ..." -ForegroundColor Cyan
Push-Location $repoRoot
dotnet run --project tools/PatternPro.DbTool -- verify-connection 2>&1
$code = $LASTEXITCODE
Pop-Location

if ($code -ne 0) {
    Write-Host ""
    Write-Host "Fix Pattern.Web/appsettings.Development.json (Postgres on this PC uses Host=localhost)." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "4) Seed / admin (once on server PC):" -ForegroundColor Cyan
Write-Host "   dotnet run --project tools/PatternPro.DbTool -- sync"
Write-Host "   dotnet run --project tools/PatternPro.DbTool -- reset-admin-password"
Write-Host ""
Write-Host "5) On PC 2, 3, 4 run:" -ForegroundColor Cyan
Write-Host "   powershell -ExecutionPolicy Bypass -File tools/setup-desktop-client.ps1 -ServerHost $($ips | Select-Object -First 1)"
Write-Host ""
Write-Host "See docs/MULTI_PC_SETUP.md for full steps." -ForegroundColor DarkGray
Write-Host ""
