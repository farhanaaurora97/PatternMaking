# PatternPro Desktop — startup smoke + manual checklist
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools/qa-desktop-test.ps1
#   powershell -ExecutionPolicy Bypass -File tools/qa-desktop-test.ps1 -LaunchOnly
#   powershell -ExecutionPolicy Bypass -File tools/qa-desktop-test.ps1 -SkipBuild

param(
    [switch]$LaunchOnly,
    [switch]$SkipBuild,
    [int]$LaunchSeconds = 6
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot "PatternPro.Desktop/PatternPro.Desktop.csproj"
$outDir = Join-Path $repoRoot "PatternPro.Desktop/bin/Release/net8.0-windows10.0.19041.0/win10-x64"
$exe = Join-Path $outDir "PatternPro.Desktop.exe"
$webDev = Join-Path $repoRoot "Pattern.Web/appsettings.Development.json"

$script:Passed = 0
$script:Failed = 0

function Pass([string]$Name, [string]$Detail = "") {
    $script:Passed++
    $line = "  PASS  $Name"
    if ($Detail) { $line += " - $Detail" }
    Write-Host $line -ForegroundColor Green
}

function Fail([string]$Name, [string]$Detail = "") {
    $script:Failed++
    $line = "  FAIL  $Name"
    if ($Detail) { $line += " - $Detail" }
    Write-Host $line -ForegroundColor Red
}

function Try-Test([string]$Name, [scriptblock]$Block) {
    try { & $Block }
    catch { Fail $Name $_.Exception.Message }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PatternPro Desktop Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not $LaunchOnly) {
    Try-Test "DT0 Web dev config" {
        if (-not (Test-Path $webDev)) { throw "Missing $webDev (Postgres connection for Desktop pilot)" }
        Pass "DT0 Web dev config" $webDev
    }

    Try-Test "DT1 Reset admin password" {
        Push-Location $repoRoot
        $out = dotnet run --project tools/PatternPro.DbTool -- reset-admin-password 2>&1
        Pop-Location
        if ($out -notmatch "Reset password for 'admin'") { throw "reset-admin-password failed: $out" }
        Pass "DT1 Admin password" "admin / Admin@123"
    }

    if (-not $SkipBuild) {
        Try-Test "DT2 Build Desktop (Release)" {
            Push-Location $repoRoot
            dotnet build $project -c Release --no-restore 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
            Pop-Location
            Pass "DT2 Build" "Release"
        }
    }

    Try-Test "DT3 Desktop exe exists" {
        if (-not (Test-Path $exe)) { throw "Missing $exe" }
        Pass "DT3 Exe" $exe
    }

    Try-Test "DT4 WebView2 loader" {
        $loader = Join-Path $outDir "WebView2Loader.dll"
        if (-not (Test-Path $loader)) { throw "Missing WebView2Loader.dll" }
        Pass "DT4 WebView2Loader"
    }
}

Get-Process -Name PatternPro.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

Try-Test "DT5 Launch smoke" {
    $proc = Start-Process -FilePath $exe -WorkingDirectory $outDir -PassThru
    Start-Sleep -Seconds $LaunchSeconds
    if ($proc.HasExited) { throw "Process exited immediately (code $($proc.ExitCode))" }
    Pass "DT5 Launch smoke" "PID $($proc.Id), running ${LaunchSeconds}s"
    if (-not $LaunchOnly) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 1
    }
}

if (-not $LaunchOnly) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  DESKTOP STARTUP SUMMARY" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Passed:  $($script:Passed)" -ForegroundColor $(if ($script:Failed -eq 0) { "Green" } else { "Yellow" })
    Write-Host "  Failed:  $($script:Failed)" -ForegroundColor $(if ($script:Failed -eq 0) { "Green" } else { "Red" })
    Write-Host ""
}

Write-Host "Manual UI checklist (walk through in the app window):" -ForegroundColor Cyan
Write-Host "  Login: auto sign-in as admin on local pilot (Dashboard expected)"
Write-Host "   1. Dashboard     - + New style -> Create -> toast + new row"
Write-Host "   2. Style Sheet   - + New style; change lifecycle pill -> saves"
Write-Host "   3. Size Chart    - + Add measurement / + Add size -> dialog works"
Write-Host "   4. Block Gen     - Generate block -> green banner"
Write-Host "   5. Grading       - Edit delta cell -> persists after refresh"
Write-Host "   6. Pieces        - Select pattern -> Generate -> grid + toast"
Write-Host "   7. Pieces        - + Add Piece -> wide dialog + piece added"
Write-Host "   8. Canvas        - Move vertex -> Save All -> persists"
Write-Host "   9. Export        - QC -> Approve -> Record pass -> factory enabled"
Write-Host "  10. Export        - Factory export DXF/HPGL/PLT -> ZIP + toast"
Write-Host "  11. Admin         - + New user -> Create -> toast + row"
Write-Host "  12. Account       - Change password -> success message"
Write-Host ""
Write-Host "Launch only: powershell -ExecutionPolicy Bypass -File tools/qa-desktop-test.ps1 -LaunchOnly" -ForegroundColor DarkGray
Write-Host ""

if ($script:Failed -gt 0) { exit 1 }
exit 0
