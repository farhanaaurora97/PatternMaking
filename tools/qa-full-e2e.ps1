# PatternPro FULL end-to-end test (first to last)
# Usage: powershell -ExecutionPolicy Bypass -File tools/qa-full-e2e.ps1

param(
    [string]$BaseUrl = "http://localhost:5001",
    [string]$UserName = "admin",
    [string]$Password = "Admin@123"
)

$ErrorActionPreference = "Continue"
$script:Passed = 0
$script:Failed = 0
$script:Errors = [System.Collections.Generic.List[string]]::new()
$script:Warnings = [System.Collections.Generic.List[string]]::new()

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
    $script:Errors.Add("$Name : $Detail")
}

function Warn([string]$Msg) {
    Write-Host "  WARN  $Msg" -ForegroundColor Yellow
    $script:Warnings.Add($Msg)
}

function Try-Test([string]$Name, [scriptblock]$Block) {
    try { & $Block }
    catch { Fail $Name $_.Exception.Message }
}

function Login-Admin {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $loginPage = Invoke-WebRequest -Uri "$BaseUrl/Account/Login" -WebSession $session -UseBasicParsing
    $m = [regex]::Match($loginPage.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $m.Success) { throw "Login anti-forgery token not found" }
    $body = @{
        UserName                   = $UserName
        Password                   = $Password
        RememberMe                 = "false"
        ReturnUrl                  = ""
        __RequestVerificationToken = $m.Groups[1].Value
    }
    Invoke-WebRequest -Uri "$BaseUrl/Account/Login" -Method POST -Body $body -WebSession $session -UseBasicParsing | Out-Null
    return $session
}

function Get-PieceList($data) {
    if ($data -is [System.Array]) { return @($data) }
    if ($null -ne $data.pieces) { return @($data.pieces) }
    return @($data)
}

. "$PSScriptRoot/qa-helpers.ps1"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PatternPro FULL E2E Test (first-last)" -ForegroundColor Cyan
Write-Host "  Target: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# --- A. AUTH ---
Write-Host "--- A. Authentication ---" -ForegroundColor Cyan

Try-Test "A1 Login page loads" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Account/Login" -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    Pass "A1 Login page loads"
}

Try-Test "A2 Registration is closed" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Account/Register" -MaximumRedirection 0 -UseBasicParsing -ErrorAction SilentlyContinue
    if ($r.StatusCode -ne 302) { throw "Expected redirect, got $($r.StatusCode)" }
    if ($r.Headers.Location -notmatch "Login") { throw "Expected redirect to Login" }
    Pass "A2 Registration is closed" "redirects to login"
}

$session = $null
Try-Test "A3 Admin login" {
    $script:session = Login-Admin
    Pass "A3 Admin login" $UserName
}

if (-not $session) {
    Write-Host ""
    Write-Host "STOPPED: Cannot login. Is the app running on $BaseUrl ?" -ForegroundColor Red
    exit 1
}

Try-Test "A4 Admin panel loads" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Admin" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    Pass "A4 Admin panel loads"
}

Try-Test "A5 User panel loads" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/User" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    Pass "A5 User panel loads"
}

# --- B. SETUP DATA ---
Write-Host ""
Write-Host "--- B. Size chart & setup ---" -ForegroundColor Cyan

Try-Test "B1 Size Chart page" {
    Invoke-WebRequest -Uri "$BaseUrl/SizeChart" -WebSession $session -UseBasicParsing | Out-Null
    Pass "B1 Size Chart page"
}

Try-Test "B2 Size chart M waist = 84 cm" {
    Ensure-SizeChartWaistM -Session $session -BaseUrl $BaseUrl -Value 84
    $mWaist = Get-WaistMValue (Get-SizeChartCsv $BaseUrl $session)
    if ($mWaist -ne 84) { throw "Waist M not 84: $mWaist" }
    Pass "B2 Size chart M waist = 84 cm"
}

Try-Test "B3 Block Generator page" {
    Invoke-WebRequest -Uri "$BaseUrl/BlockGenerator?style=slim" -WebSession $session -UseBasicParsing | Out-Null
    Pass "B3 Block Generator page"
}

Try-Test "B4 Grading page" {
    Invoke-WebRequest -Uri "$BaseUrl/Grading?style=slim" -WebSession $session -UseBasicParsing | Out-Null
    Pass "B4 Grading page"
}

# --- C. CREATE PATTERN ---
Write-Host ""
Write-Host "--- C. Create pattern ---" -ForegroundColor Cyan

$script:patternId = 0
Try-Test "C1 Create pattern (Dashboard API)" {
    $body = @{
        name            = "E2E Full $(Get-Date -Format 'yyyyMMdd-HHmmss')"
        styleKey        = "slim"
        baseSize        = "M"
        categoryKey     = "denim"
        designer        = "E2E Tester"
        season          = "SS26"
        owner           = "QA"
        lifecycleStatus = "Idea"
    }
    $created = Post-Json "$BaseUrl/Home/Create" $session $body
    $script:patternId = [int]$created.id
    if ($script:patternId -le 0) { throw "No pattern id" }
    Pass "C1 Create pattern" "id=$($script:patternId) code=$($created.code)"
}

if ($script:patternId -le 0) {
    Write-Host "STOPPED: Pattern create failed." -ForegroundColor Red
    exit 1
}
$patternId = $script:patternId

# --- D. PIECES & CANVAS ---
Write-Host ""
Write-Host "--- D. Pattern pieces & canvas ---" -ForegroundColor Cyan

Try-Test "D1 Draft pieces from measurements" {
    Invoke-WebRequest -Uri "$BaseUrl/Pieces/DraftPieces?patternId=$patternId&style=slim" -Method POST -WebSession $session -UseBasicParsing | Out-Null
    Pass "D1 Draft pieces from measurements"
}

Try-Test "D2 Pattern Pieces page" {
    Invoke-WebRequest -Uri "$BaseUrl/Pieces?patternId=$patternId&style=slim" -WebSession $session -UseBasicParsing | Out-Null
    Pass "D2 Pattern Pieces page"
}

Try-Test "D3 Canvas page" {
    Invoke-WebRequest -Uri "$BaseUrl/Canvas?patternId=$patternId&style=slim" -WebSession $session -UseBasicParsing | Out-Null
    Pass "D3 Canvas page"
}

Try-Test "D4 Canvas has required pieces" {
    $data = Invoke-RestMethod -Uri "$BaseUrl/Canvas/PieceData?patternId=$patternId&style=slim" -WebSession $session
    $pieces = Get-PieceList $data
    $names = @($pieces | ForEach-Object {
        if ($_.name) { $_.name } elseif ($_.Name) { $_.Name } else { "" }
    })
    foreach ($req in @("Front Leg", "Back Leg", "Waistband")) {
        if ($names -notcontains $req) { throw "Missing piece: $req (found: $($names -join ', '))" }
    }
    Pass "D4 Canvas has required pieces" "$($names.Count) pieces"
}

Try-Test "D5 Graded Nest page" {
    Invoke-WebRequest -Uri "$BaseUrl/Nest?style=slim" -WebSession $session -UseBasicParsing | Out-Null
    Pass "D5 Graded Nest page"
}

Try-Test "D6 Library page" {
    Invoke-WebRequest -Uri "$BaseUrl/Library" -WebSession $session -UseBasicParsing | Out-Null
    Pass "D6 Library page"
}

Try-Test "D7 Style Sheet page" {
    Invoke-WebRequest -Uri "$BaseUrl/StyleSheet" -WebSession $session -UseBasicParsing | Out-Null
    Pass "D7 Style Sheet page"
}

# --- E. EXPORT DRAFT ---
Write-Host ""
Write-Host "--- E. Draft export ---" -ForegroundColor Cyan

foreach ($fmt in @("PLT", "DXF", "HPGL", "PDF")) {
    Try-Test "E1 Draft export $fmt" {
        $r = Invoke-WebRequest -Uri "$BaseUrl/Export/DownloadPackage?patternId=$patternId&style=slim&format=$fmt&purpose=draft&sizes=M" -WebSession $session -UseBasicParsing
        if ($r.RawContentLength -lt 50) { throw "File too small" }
        Pass "E1 Draft export $fmt" "$($r.RawContentLength) bytes"
    }
}

# --- F. FACTORY QC & CERTIFICATION ---
Write-Host ""
Write-Host "--- F. Factory QC & certification ---" -ForegroundColor Cyan

$qc = $null
Try-Test "F1 Factory QC JSON" {
    $qc = Invoke-RestMethod -Uri "$BaseUrl/Export/ValidateFactory?patternId=$patternId&style=slim" -WebSession $session
    $blocking = @($qc.issues | Where-Object { $_.code -notin @("NOT_APPROVED", "CUTTER_TEST") })
    if ($blocking.Count -gt 0) {
        $detail = ($blocking | ForEach-Object { $_.code + ": " + $_.message }) -join "; "
        throw "Blocking QC: $detail"
    }
    Pass "F1 Factory QC JSON" "blocking issues=0, warnings=$($qc.warnings.Count)"
}

Try-Test "F2 Approve for cutting" {
    try {
        $resp = Post-Json "$BaseUrl/Export/ApproveForCutting" $session @{ patternId = $patternId; style = "slim"; actor = "E2E Tester" }
        if (-not $resp.approvedForCutting) { throw "Approve did not set flag" }
        Pass "F2 Approve for cutting"
    }
    catch {
        $msg = $_.Exception.Message
        if ($_.ErrorDetails.Message) { $msg = $_.ErrorDetails.Message }
        throw $msg
    }
}

Try-Test "F3 Record cutter test pass" {
    $resp = Post-Json "$BaseUrl/Export/RecordCutterTest" $session @{
        patternId = $patternId
        passed    = $true
        actor     = "E2E Factory"
        notes     = "Automated E2E test"
    }
    if (-not $resp.cutterTestPassed) { throw "Cutter test flag not set" }
    Pass "F3 Record cutter test pass"
}

Try-Test "F4 Factory QC after certification" {
    $qc2 = Invoke-RestMethod -Uri "$BaseUrl/Export/ValidateFactory?patternId=$patternId&style=slim" -WebSession $session
    if (-not $qc2.canExportToFactory) {
        $issues = ($qc2.issues | ForEach-Object { $_.code }) -join ", "
        throw "canExportToFactory=false, issues: $issues"
    }
    Pass "F4 Factory QC after certification" "canExport=true"
}

Try-Test "F5 Factory export ZIP" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Export/DownloadPackage?patternId=$patternId&style=slim&format=PLT&purpose=factory&sizes=M" -WebSession $session -UseBasicParsing
    if ($r.RawContentLength -lt 100) { throw "ZIP too small" }
    $zipPath = Join-Path $env:TEMP "patternpro-e2e-$patternId.zip"
    [IO.File]::WriteAllBytes($zipPath, $r.Content)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $cert = $zip.Entries | Where-Object { $_.Name -eq "certification.json" } | Select-Object -First 1
        if (-not $cert) { throw "certification.json missing from factory ZIP" }
        Pass "F5 Factory export ZIP" "$([math]::Round($r.RawContentLength/1KB,1)) KB"
    }
    finally {
        $zip.Dispose()
        Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    }
}

Try-Test "F6 Export page loads" {
    Invoke-WebRequest -Uri "$BaseUrl/Export?patternId=$patternId&style=slim" -WebSession $session -UseBasicParsing | Out-Null
    Pass "F6 Export page loads"
}

# --- G. DASHBOARD ---
Write-Host ""
Write-Host "--- G. Dashboard ---" -ForegroundColor Cyan

Try-Test "G1 Dashboard loads" {
    Invoke-WebRequest -Uri "$BaseUrl/" -WebSession $session -UseBasicParsing | Out-Null
    Pass "G1 Dashboard loads"
}

Try-Test "G2 Dashboard charts API" {
    Invoke-RestMethod -Uri "$BaseUrl/Home/ChartsData" -WebSession $session | Out-Null
    Pass "G2 Dashboard charts API"
}

# --- H. LOGOUT ---
Write-Host ""
Write-Host "--- H. Logout ---" -ForegroundColor Cyan

Try-Test "H1 Logout" {
    $dash = (Invoke-WebRequest -Uri "$BaseUrl/" -WebSession $session -UseBasicParsing).Content
    $m = [regex]::Match($dash, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    $body = @{ __RequestVerificationToken = $m.Groups[1].Value }
    Invoke-WebRequest -Uri "$BaseUrl/Account/Logout" -Method POST -Body $body -WebSession $session -UseBasicParsing | Out-Null
    Pass "H1 Logout"
}

# --- SUMMARY ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  E2E SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Passed:  $script:Passed" -ForegroundColor Green
Write-Host "  Failed:  $script:Failed" -ForegroundColor $(if ($script:Failed -gt 0) { "Red" } else { "Green" })
Write-Host "  Pattern tested: id=$patternId" -ForegroundColor Gray
Write-Host ""

if ($script:Errors.Count -gt 0) {
    Write-Host "ERRORS FOUND:" -ForegroundColor Red
    foreach ($e in $script:Errors) {
        Write-Host "  - $e" -ForegroundColor Red
    }
    Write-Host ""
}

if ($script:Warnings.Count -gt 0) {
    Write-Host "Warnings:" -ForegroundColor Yellow
    foreach ($w in $script:Warnings) {
        Write-Host "  - $w" -ForegroundColor Yellow
    }
    Write-Host ""
}

if ($script:Failed -gt 0) { exit 1 }
Write-Host "FULL E2E TEST PASSED - no errors." -ForegroundColor Green
Write-Host ""
exit 0
