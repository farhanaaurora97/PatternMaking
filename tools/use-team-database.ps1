# Run ON THIS (other) PC after main PC allows LAN Postgres.
# Points Desktop / Web appsettings at the shared Team database (all patterns).
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools\use-team-database.ps1
#   powershell -ExecutionPolicy Bypass -File tools\use-team-database.ps1 -HostIp 192.168.1.15 -Port 5433

param(
    [string]$HostIp = "192.168.1.15",
    [int]$Port = 5433,
    [string]$Database = "patternpro",
    [string]$Username = "postgres",
    [string]$Password = "1234"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

$conn = "Host=$HostIp;Port=$Port;Database=$Database;Username=$Username;Password=$Password"
Write-Host "Testing $HostIp`:$Port ..." -ForegroundColor Cyan

$psqlCandidates = @(
    "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    "C:\Program Files\PostgreSQL\15\bin\psql.exe",
    "C:\Program Files\PostgreSQL\14\bin\psql.exe"
)
$psql = $psqlCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($psql) {
    $env:PGPASSWORD = $Password
    $count = & $psql -h $HostIp -p $Port -U $Username -d $Database -t -A -c 'SELECT COUNT(*) FROM patternpro.patterns;' 2>&1
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    if ($LASTEXITCODE -ne 0) {
        Write-Host $count -ForegroundColor Red
        Write-Host ""
        Write-Host "Cannot read patterns from main PC yet." -ForegroundColor Yellow
        Write-Host "On the MAIN PC run (Admin):" -ForegroundColor Yellow
        Write-Host "  powershell -ExecutionPolicy Bypass -File tools\allow-lan-postgres.ps1"
        Write-Host "Then re-run this script."
        exit 1
    }
    Write-Host "Connected. Pattern count on main PC: $count" -ForegroundColor Green
} else {
    Write-Host "psql not found — writing settings anyway (start app to verify)." -ForegroundColor Yellow
}

$json = @{
    ConnectionStrings = @{ Postgres = $conn }
    Auth = @{
        SeedAdminUserName = "admin"
        SeedAdminPassword = "Admin@123"
    }
} | ConvertTo-Json -Depth 5

$targets = @(
    (Join-Path $repoRoot "Pattern.Web\appsettings.Development.json"),
    (Join-Path $repoRoot "PatternPro.Desktop\appsettings.Development.json"),
    (Join-Path $repoRoot "PatternPro.Desktop\appsettings.Team.json"),
    (Join-Path $repoRoot "dist\PatternPro-Desktop-win-x64\appsettings.Development.json"),
    (Join-Path $repoRoot "dist\PatternPro-Desktop-win-x64\appsettings.Team.json"),
    (Join-Path $env:USERPROFILE "Downloads\PatternPro-Desktop-1.0-win-x64\appsettings.Development.json"),
    (Join-Path $env:USERPROFILE "Downloads\PatternPro-Desktop-1.0-win-x64\appsettings.Team.json"),
    "E:\PatternPro-Test\PatternPro-Desktop-win-x64\appsettings.Development.json",
    "E:\PatternPro-Test\PatternPro-Desktop-win-x64\appsettings.Team.json"
)

$debugExeDir = Join-Path $repoRoot "PatternPro.Desktop\bin\Debug\net10.0-windows10.0.19041.0\win-x64"
if (Test-Path $debugExeDir) {
    $targets += (Join-Path $debugExeDir "appsettings.Development.json")
    $targets += (Join-Path $debugExeDir "appsettings.Team.json")
}

foreach ($path in $targets) {
    $dir = Split-Path $path -Parent
    if (-not (Test-Path $dir)) { continue }
    Set-Content -Path $path -Value $json -Encoding UTF8
    Write-Host "Updated $path" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Done. Restart PatternPro Desktop (close all windows, then open again)." -ForegroundColor Green
Write-Host "Dashboard should show the same pattern count as the main PC." -ForegroundColor Green
