# Configure PatternPro Desktop on PC 2, 3, 4 to use shared PostgreSQL on PC 1.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools/setup-desktop-client.ps1
#   powershell -ExecutionPolicy Bypass -File tools/setup-desktop-client.ps1 -ServerHost 192.168.1.10 -Password 1234
#   powershell -ExecutionPolicy Bypass -File tools/setup-desktop-client.ps1 -TargetDir "C:\PatternPro"

param(
    [string]$ServerHost = "",
    [int]$Port = 5433,
    [string]$Database = "patternpro",
    [string]$Username = "postgres",
    [string]$Password = "",
    [string]$TargetDir = ""
)

$ErrorActionPreference = "Stop"
function Resolve-RepoRoot {
    $dir = $PSScriptRoot
    if (Test-Path (Join-Path $dir "PatternPro.sln")) { return $dir }
    $parent = Split-Path $dir -Parent
    if (Test-Path (Join-Path $parent "PatternPro.sln")) { return $parent }
    return $dir
}

$repoRoot = Resolve-RepoRoot

function Resolve-TargetDir {
    if ($TargetDir) { return $TargetDir }

    if (Test-Path (Join-Path $PSScriptRoot "PatternPro.Desktop.exe")) { return $PSScriptRoot }

    $dist = Join-Path $repoRoot "dist/PatternPro-Desktop-win-x64"
    if (Test-Path (Join-Path $dist "PatternPro.Desktop.exe")) { return $dist }

    $build = Join-Path $repoRoot "PatternPro.Desktop/bin/Release/net8.0-windows10.0.19041.0/win10-x64"
    if (Test-Path (Join-Path $build "PatternPro.Desktop.exe")) { return $build }

    throw "PatternPro.Desktop.exe not found. Pass -TargetDir or publish: tools/publish-desktop-windows.ps1"
}

function Read-Secret([string]$Prompt) {
    $secure = Read-Host $Prompt -AsSecureString
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

Write-Host ""
Write-Host "=== PatternPro — Desktop client setup (PC 2, 3, 4) ===" -ForegroundColor Cyan
Write-Host ""

if (-not $ServerHost) {
    $ServerHost = Read-Host "PostgreSQL server IP (PC 1, e.g. 192.168.1.10)"
}
if ([string]::IsNullOrWhiteSpace($ServerHost)) { throw "ServerHost is required." }

if (-not $Password) {
    $Password = Read-Secret "PostgreSQL password for user '$Username'"
}
if ([string]::IsNullOrWhiteSpace($Password)) { throw "Password is required." }

$dir = Resolve-TargetDir
$teamFile = Join-Path $dir "appsettings.Team.json"

$settings = @{
    ConnectionStrings = @{
        Postgres = "Host=$ServerHost;Port=$Port;Database=$Database;Username=$Username;Password=$Password"
    }
    Auth = @{
        SeedAdminUserName = "admin"
        SeedAdminPassword = "Admin@123"
    }
}
$settings | ConvertTo-Json -Depth 4 | Set-Content -Path $teamFile -Encoding UTF8
Write-Host "Wrote: $teamFile" -ForegroundColor Green
Write-Host "  Host=$ServerHost Port=$Port Database=$Database" -ForegroundColor DarkGray

Write-Host ""
Write-Host "Testing connection from this PC ..." -ForegroundColor Cyan
$connStr = $settings.ConnectionStrings.Postgres
$testOk = $false

if (Test-Path (Join-Path $repoRoot "PatternPro.sln")) {
    $env:ConnectionStrings__Postgres = $connStr
    Push-Location $repoRoot
    dotnet run --project tools/PatternPro.DbTool -- verify-connection 2>&1
    $testOk = ($LASTEXITCODE -eq 0)
    Remove-Item Env:ConnectionStrings__Postgres -ErrorAction SilentlyContinue
    Pop-Location
}
else {
    $tcp = Test-NetConnection -ComputerName $ServerHost -Port $Port -WarningAction SilentlyContinue
    if ($tcp.TcpTestSucceeded) {
        Write-Host "[Setup] OK — TCP $Port reachable on $ServerHost (full DB test needs repo + DbTool)." -ForegroundColor Green
        $testOk = $true
    }
    else {
        Write-Host "[Setup] FAILED — cannot reach ${ServerHost}:${Port}. Check PC 1 firewall and PostgreSQL." -ForegroundColor Red
    }
}
if (-not $testOk) {
    Write-Host ""
    Write-Host "Connection failed. On PC 1 run tools/setup-postgres-server.ps1 and check firewall/pg_hba.conf." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "OK — start Desktop from:" -ForegroundColor Green
Write-Host "  $dir\PatternPro.Desktop.exe"
Write-Host ""
Write-Host "Login: admin / Admin@123 (change in Admin after first sign-in)." -ForegroundColor DarkGray
Write-Host ""
