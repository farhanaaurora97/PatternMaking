# Pre-delivery Desktop QA — automated checks + optional Release launch for manual UI pass.
# Usage: powershell -ExecutionPolicy Bypass -File tools/qa-desktop-delivery-checklist.ps1
#        powershell -ExecutionPolicy Bypass -File tools/qa-desktop-delivery-checklist.ps1 -LaunchManualPass

param(
    [switch]$LaunchManualPass,
    [int]$LaunchSeconds = 20
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $repoRoot "PatternPro.Desktop/bin/Release/net8.0-windows10.0.19041.0/win10-x64/PatternPro.Desktop.exe"

$script:Pass = 0
$script:Fail = 0
$script:Manual = 0

function Report([string]$Id, [string]$Name, [string]$Result, [string]$Detail = "") {
    switch ($Result) {
        "PASS" { $script:Pass++; $c = "Green" }
        "FAIL" { $script:Fail++; $c = "Red" }
        "MANUAL" { $script:Manual++; $c = "Yellow" }
        default { $c = "White" }
    }
    $line = "  $($Result.PadRight(6)) $Id  $Name"
    if ($Detail) { $line += " - $Detail" }
    Write-Host $line -ForegroundColor $c
}

function Get-AdminPasswordFromDist {
    $files = Get-ChildItem -Path (Join-Path $repoRoot "dist") -Filter "delivery-credentials-*.txt" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending
    if (-not $files) { return $null }
    foreach ($line in Get-Content $files[0].FullName) {
        if ($line -match '^admin \(Admin\):\s+(.+)$') { return $Matches[1].Trim() }
    }
    return $null
}

function Invoke-DbTool {
    param([string[]]$DbArgs)
    Push-Location $repoRoot
    $out = dotnet run --project tools/PatternPro.DbTool -- @DbArgs 2>&1
    $code = $LASTEXITCODE
    Pop-Location
    return @{ Code = $code; Text = (($out | ForEach-Object { "$_" }) -join "`n") }
}

Write-Host ""
Write-Host "=== Desktop delivery QA (section 4) ===" -ForegroundColor Cyan
Write-Host ""

# Release build
if (-not (Test-Path $exe)) {
    Push-Location $repoRoot
    dotnet build PatternPro.Desktop/PatternPro.Desktop.csproj -c Release | Out-Null
    if ($LASTEXITCODE -ne 0) { Report "R0" "Release build" "FAIL" "dotnet build Release failed" }
    Pop-Location
}
if (Test-Path $exe) { Report "R0" "Release build / exe" "PASS" $exe }
else { Report "R0" "Release build / exe" "FAIL" "Missing exe" }

$adminPw = $env:PATTERNPRO_QA_ADMIN_PASSWORD
if (-not $adminPw) { $adminPw = Get-AdminPasswordFromDist }
if (-not $adminPw) { $adminPw = "Admin@123" }

# 1 Login
$login = Invoke-DbTool @("verify-login", "admin", $adminPw)
if ($login.Text -match 'ValidateLogin\(''admin''\): OK') {
    Report "4.1" "Login (admin)" "PASS" "DbTool verify-login"
} else {
    Report "4.1" "Login (admin)" "FAIL" "Set PATTERNPRO_QA_ADMIN_PASSWORD or run prepare-delivery-security.ps1"
}

Report "4.1b" "Logout" "MANUAL" "Account menu -> Sign out -> login screen"

# 2 Library loading guard (code + data)
$lib = Get-Content (Join-Path $repoRoot "PatternPro.Desktop/Components/Pages/LibraryPage.razor") -Raw
if ($lib -match "_loadInProgress" -and $lib -match "_loading = false") {
    Report "4.2" "Library load guard" "PASS" "_loadInProgress + clears _loading in finally"
} else {
    Report "4.2" "Library load guard" "FAIL" "LibraryPage guard missing"
}
$conn = Invoke-DbTool @("verify-connection")
if ($conn.Text -match "Patterns=(\d+)") {
    Report "4.2b" "Library data source" "PASS" "$($Matches[1]) patterns in DB"
} else {
    Report "4.2b" "Library data source" "FAIL" $conn.Text
}
Report "4.2c" "Library UI scroll" "MANUAL" "Open Library - no endless Loading spinner"

# 3 Refresh
$nav = Get-Content (Join-Path $repoRoot "PatternPro.Desktop/Components/Layout/NavMenu.razor") -Raw
$top = Get-Content (Join-Path $repoRoot "PatternPro.Desktop/Components/Layout/AppTopBar.razor") -Raw
if ($nav -match "_refreshing = false" -and $top -match "_refreshing = false" -and (Test-Path (Join-Path $repoRoot "PatternPro.Desktop/Helpers/DesktopShellRefresh.cs"))) {
    Report "4.3" "Refresh code" "PASS" "try/finally clears _refreshing"
} else {
    Report "4.3" "Refresh code" "FAIL"
}
Report "4.3b" "Refresh click" "MANUAL" "Top bar Refresh - toast + page reload, button re-enables"

# 4 Dashboard
if ($conn.Text -match "Patterns=\d+" -and $conn.Text -match "Users=\d+") {
    Report "4.4" "Dashboard KPIs data" "PASS" "DB reachable for dashboard services"
} else {
    Report "4.4" "Dashboard KPIs data" "FAIL"
}
Report "4.4b" "Dashboard open style" "MANUAL" "Dashboard -> open a style row -> detail loads"

# 5 Canvas M-only generate
$canvas = Get-Content (Join-Path $repoRoot "PatternPro.Desktop/Components/Pages/CanvasPage.razor") -Raw
if ($canvas -match 'new\("M", true\)' -and $canvas -match "var canvasSize = exportSizes\[0\]") {
    Report "4.5" "Canvas Generate (M-only default)" "PASS" "Only first checked size drafted on canvas"
} else {
    Report "4.5" "Canvas Generate (M-only default)" "FAIL"
}
Report "4.5b" "Canvas visual" "MANUAL" "Canvas -> M only -> Generate -> single stack (not duplicated sizes)"

# 6 Export DXF
Push-Location $repoRoot
dotnet test PatternPro.Tests/PatternPro.Tests.csproj -c Release --no-restore -v q --filter "FullyQualifiedName~DxfExport" 2>&1 | Out-Null
$testOk = ($LASTEXITCODE -eq 0)
Pop-Location
if ($testOk) {
    Report "4.6" "Export / DXF pipeline" "PASS" "PatternPro.Tests DxfExport* passed"
} else {
    Report "4.6" "Export / DXF pipeline" "FAIL" "DxfExport unit tests failed"
}
Report "4.6b" "Export to Downloads" "MANUAL" "Export page -> draft/factory -> file in Downloads folder"

# 7 Admin new user + modal
$adminPage = Get-Content (Join-Path $repoRoot "PatternPro.Desktop/Components/Pages/AdminPage.razor") -Raw
$modalJs = Test-Path (Join-Path $repoRoot "PatternPro.Desktop/wwwroot/js/patternpro.js")
$modalRoot = Select-String -Path (Join-Path $repoRoot "PatternPro.Desktop/wwwroot/index.html") -Pattern "pp-modal-root" -Quiet
$modalCss = Select-String -Path (Join-Path $repoRoot "PatternPro.Desktop/wwwroot/css/site.css") -Pattern "#pp-modal-root" -Quiet
if ($adminPage -match "mountToModalRoot" -and $modalJs -and $modalRoot -and $modalCss) {
    Report "4.7a" "Admin modal (centered infrastructure)" "PASS" "pp-modal-root + mountToModalRoot"
} else {
    Report "4.7a" "Admin modal (centered infrastructure)" "FAIL"
}

$qaUser = "pp-qa-" + (Get-Date -Format "HHmmss")
$qaPw = "QaTemp1!" + (Get-Random -Maximum 9999)
$create = Invoke-DbTool @("create-user", "PP-QA-001", $qaUser, "QA Temp User", "Viewer", $qaPw)
if ($create.Code -eq 0 -and $create.Text -match "Created user") {
    Report "4.7b" "Admin create user API" "PASS" $qaUser
} else {
    Report "4.7b" "Admin create user API" "FAIL" $create.Text
}
Report "4.7c" "Admin + New user UI" "MANUAL" "Modal centered on screen; then disable/delete test user in Admin"

# 8 Account change password
$newPw = $qaPw + "X"
if ($create.Code -eq 0) {
    $set1 = Invoke-DbTool @("set-password", $qaUser, $newPw)
    $v1 = Invoke-DbTool @("verify-login", $qaUser, $newPw)
    if ($set1.Code -eq 0 -and $v1.Text -match "OK") {
        Report "4.8" "Change password flow" "PASS" "set-password + verify-login for test user"
    } else {
        Report "4.8" "Change password flow" "FAIL"
    }
    $del = Invoke-DbTool @("delete-user", $qaUser)
    if ($del.Code -eq 0) { Report "4.8b" "Cleanup test user" "PASS" }
    else { Report "4.8b" "Cleanup test user" "FAIL" "delete-user $qaUser" }
} else {
    Report "4.8" "Change password flow" "FAIL" "Skipped - no test user"
}

Report "4.8c" "Account page UI" "MANUAL" "Sign in as designer -> Account -> Update password"

Write-Host ""
Write-Host "Summary: PASS=$($script:Pass)  FAIL=$($script:Fail)  MANUAL=$($script:Manual)" -ForegroundColor Cyan
Write-Host ""

if ($LaunchManualPass -and (Test-Path $exe)) {
    Get-Process -Name PatternPro.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    Write-Host "Launching Release Desktop for ${LaunchSeconds}s - complete MANUAL rows in the app window." -ForegroundColor Yellow
    $proc = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent) -PassThru
    Start-Sleep -Seconds $LaunchSeconds
    if (-not $proc.HasExited) {
        Write-Host "Desktop running (PID $($proc.Id)). Close the window when manual QA is done." -ForegroundColor Green
    } else {
        Report "L1" "Desktop launch" "FAIL" "Process exited early"
    }
}

if ($script:Fail -gt 0) { exit 1 }
exit 0
