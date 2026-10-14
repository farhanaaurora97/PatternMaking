# Step 6: simulate clean-machine install from team ZIP (extract outside repo, smoke run).
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools/qa-customer-sim-test.ps1
#   powershell -ExecutionPolicy Bypass -File tools/qa-customer-sim-test.ps1 -SkipPublish

param(
    [switch]$SkipPublish,
    [string]$PostgresPassword,
    [int]$LaunchSeconds = 12,
    [string]$ExtractRoot = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$zipPath = Join-Path $repoRoot "dist/PatternPro-Desktop-1.0-win-x64.zip"
$publishScript = Join-Path $repoRoot "tools/publish-team-package.ps1"
$devSettings = Join-Path $repoRoot "Pattern.Web/appsettings.Development.json"

if (-not $PostgresPassword -and (Test-Path $devSettings)) {
    $dev = Get-Content $devSettings -Raw | ConvertFrom-Json
    $conn = [string]$dev.ConnectionStrings.Postgres
    if ($conn -match 'Password=([^;]+)') { $PostgresPassword = $Matches[1] }
}

if (-not $PostgresPassword) {
    throw "Postgres password required: -PostgresPassword or Pattern.Web/appsettings.Development.json"
}

Get-Process -Name PatternPro.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

if (-not $SkipPublish) {
    Write-Host "Building team ZIP..." -ForegroundColor Cyan
    & $publishScript -PostgresPassword $PostgresPassword -SkipLaunchTest
    if ($LASTEXITCODE -ne 0) { throw "publish-team-package.ps1 failed (exit $LASTEXITCODE)" }
}

if (-not (Test-Path $zipPath)) { throw "Missing ZIP: $zipPath" }

if ([string]::IsNullOrWhiteSpace($ExtractRoot)) {
    $ExtractRoot = Join-Path $env:TEMP "PatternPro-customer-sim-$(Get-Date -Format yyyyMMddHHmmss)"
}

if (Test-Path $ExtractRoot) { Remove-Item -Recurse -Force $ExtractRoot }
New-Item -ItemType Directory -Force -Path $ExtractRoot | Out-Null

Write-Host "Extracting ZIP to clean folder (not repo):" -ForegroundColor Cyan
Write-Host "  $ExtractRoot"
Expand-Archive -Path $zipPath -DestinationPath $ExtractRoot -Force

$appDir = Get-ChildItem -Path $ExtractRoot -Directory | Select-Object -First 1
if (-not $appDir) { throw "ZIP extract produced no folder under $ExtractRoot" }
$exe = Join-Path $appDir.FullName "PatternPro.Desktop.exe"
if (-not (Test-Path $exe)) { throw "Missing exe in $($appDir.FullName)" }

$script:FailCount = 0
function Check([string]$Name, [bool]$Ok, [string]$Detail = "") {
    if ($Ok) { Write-Host "  PASS  $Name $(if ($Detail) { "- $Detail" })" -ForegroundColor Green }
    else { Write-Host "  FAIL  $Name $(if ($Detail) { "- $Detail" })" -ForegroundColor Red; $script:FailCount++ }
}

Write-Host ""
Write-Host "=== Customer sim (step 6) ===" -ForegroundColor Cyan
Write-Host ""

$devJson = Join-Path $appDir.FullName "appsettings.Development.json"
$teamJson = Join-Path $appDir.FullName "appsettings.Team.json"
$appJson = Join-Path $appDir.FullName "appsettings.json"

Check "No appsettings.Development.json in package" (-not (Test-Path $devJson))
Check "appsettings.Team.json present" (Test-Path $teamJson)

$settingsText = if (Test-Path $teamJson) { Get-Content $teamJson -Raw } else { Get-Content $appJson -Raw }
Check "Uses PostgreSQL connection string" ($settingsText -match "ConnectionStrings" -and $settingsText -match "Postgres")
Check "Not empty Postgres string" ($settingsText -match "Host=" -and $settingsText -notmatch 'Postgres"\s*:\s*""')

$hostVal = $null
if ($settingsText -match 'Host=([^;"]+)') { $hostVal = $Matches[1] }
$isTeamHost = [bool]$hostVal -and $hostVal -notmatch '^(localhost|127\.0\.0\.1)$'
Check "Team Host is server IP (not localhost)" $isTeamHost "Host=$hostVal"

Check "No default Admin@123 in shipped Auth" ($settingsText -notmatch "Admin@123")

Push-Location $repoRoot
$conn = dotnet run --project tools/PatternPro.DbTool -- verify-connection 2>&1
Pop-Location
$connText = ($conn | ForEach-Object { "$_" }) -join "`n"
Check "PC1 PostgreSQL reachable from this machine" ($connText -match "OK")

Write-Host ""
Write-Host "Launching app from extracted folder (${LaunchSeconds}s)..." -ForegroundColor Yellow
$logBefore = @()
$startupLog = Join-Path $env:LOCALAPPDATA "PatternPro/startup-error.txt"
if (Test-Path $startupLog) { $logBefore = Get-Content $startupLog -ErrorAction SilentlyContinue }

$proc = Start-Process -FilePath $exe -WorkingDirectory $appDir.FullName -PassThru
Start-Sleep -Seconds $LaunchSeconds

if ($proc.HasExited) {
    Check "App stays running on launch" $false "exit $($proc.ExitCode)"
    if (Test-Path $startupLog) {
        Write-Host "startup-error.txt:" -ForegroundColor Red
        Get-Content $startupLog -Tail 30
    }
} else {
    Check "App stays running on launch" $true "PID $($proc.Id)"
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Extract folder (simulate other PC): $ExtractRoot" -ForegroundColor DarkGray
Write-Host "Manual on real 2nd PC: Login -> Refresh -> Canvas -> Export once." -ForegroundColor Yellow
Write-Host ""

if ($script:FailCount -gt 0) { exit 1 }
exit 0
