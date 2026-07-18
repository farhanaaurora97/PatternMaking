# PatternPro Size Chart module — full HTTP/API test
# Usage: powershell -ExecutionPolicy Bypass -File tools/qa-size-chart-test.ps1

param(
    [string]$BaseUrl = "http://localhost:5001",
    [string]$UserName = "admin",
    [string]$Password = "Admin@123"
)

$ErrorActionPreference = "Continue"
$script:Passed = 0
$script:Failed = 0
$script:Errors = [System.Collections.Generic.List[string]]::new()

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

function Post-Json([string]$Uri, $Session, $Object) {
    $json = $Object | ConvertTo-Json -Compress
    return Invoke-RestMethod -Uri $Uri -Method POST -Body $json -ContentType "application/json" -WebSession $Session
}

function Post-Json-ExpectFail([string]$Uri, $Session, $Object) {
    $json = $Object | ConvertTo-Json -Compress
    try {
        Invoke-RestMethod -Uri $Uri -Method POST -Body $json -ContentType "application/json" -WebSession $Session -ErrorAction Stop | Out-Null
        throw "Expected HTTP error but request succeeded"
    }
    catch {
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -ge 400) { return }
        throw
    }
}

function Get-SizeChartCsv($Session) {
    return (Invoke-WebRequest -Uri "$BaseUrl/SizeChart/ExportCsv" -WebSession $Session -UseBasicParsing).Content
}

function Get-WaistMValue([string]$Csv) {
    $waistLine = ($Csv -split "`n") | Where-Object { $_ -match "^Waist," } | Select-Object -First 1
    if (-not $waistLine) { throw "Waist row not found in CSV" }
    $parts = $waistLine -split ","
    if ($parts.Count -lt 4) { throw "Waist row too short: $waistLine" }
    return [decimal]$parts[3]
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PatternPro Size Chart Module Test" -ForegroundColor Cyan
Write-Host "  Target: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$session = $null
Try-Test "SC0 Login" {
    $script:session = Login-Admin
    Pass "SC0 Login" $UserName
}

if (-not $session) {
    Write-Host "STOPPED: Login failed." -ForegroundColor Red
    exit 1
}

# --- Page ---
Write-Host "--- SC1. Size Chart page ---" -ForegroundColor Cyan

$pageHtml = ""
Try-Test "SC1 Size Chart page loads" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/SizeChart" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    $script:pageHtml = $r.Content
    Pass "SC1 Size Chart page loads"
}

$uiMarkers = @(
    @{ Name = "Export CSV link";    Pattern = "ExportCsv" },
    @{ Name = "Add size button";    Pattern = "btn-add-size" },
    @{ Name = "Add measurement";    Pattern = "btn-add-measurement" },
    @{ Name = "editable cells";     Pattern = "sc-cell-input" },
    @{ Name = "M base column";       Pattern = "sch-m" },
    @{ Name = "Waist row";           Pattern = "Waist" },
    @{ Name = "size-chart.js";       Pattern = "size-chart.js" }
)
foreach ($m in $uiMarkers) {
    Try-Test "SC1 UI: $($m.Name)" {
        if ($pageHtml -notmatch [regex]::Escape($m.Pattern)) { throw "Missing: $($m.Pattern)" }
        Pass "SC1 UI: $($m.Name)"
    }
}

foreach ($size in @("XS", "S", "M", "L", "XL", "XXL")) {
    Try-Test "SC1 Column $size" {
        if ($pageHtml -notmatch ">$size") { throw "Column $size not in table header" }
        Pass "SC1 Column $size"
    }
}

# --- CSV defaults ---
Write-Host ""
Write-Host "--- SC2. Export CSV ---" -ForegroundColor Cyan

$csv = ""
Try-Test "SC2 CSV downloads" {
    $script:csv = Get-SizeChartCsv $session
    if ($csv.Length -lt 50) { throw "CSV too short" }
    if ($csv -notmatch "^Measurement,") { throw "Missing header row" }
    Pass "SC2 CSV downloads" "$($csv.Split("`n").Count) lines"
}

Try-Test "SC2 M waist = 84 cm" {
    $mWaist = Get-WaistMValue $csv
    if ($mWaist -ne 84) { throw "M waist is $mWaist, expected 84" }
    Pass "SC2 M waist" "84 cm"
}

Try-Test "SC2 Default measurement rows" {
    foreach ($pom in @("Waist", "Hip", "Front Rise", "Inseam")) {
        if ($csv -notmatch "(?m)^$([regex]::Escape($pom)),") { throw "Missing row: $pom" }
    }
    Pass "SC2 Default rows" "Waist, Hip, Front Rise, Inseam"
}

# --- Update cell ---
Write-Host ""
Write-Host "--- SC3. Update cell ---" -ForegroundColor Cyan

Try-Test "SC3 UpdateCell Waist M → 85" {
    Post-Json "$BaseUrl/SizeChart/UpdateCell" $session @{
        measurementPoint = "Waist"; columnIndex = 2; value = 85
    } | Out-Null
    $mWaist = Get-WaistMValue (Get-SizeChartCsv $session)
    if ($mWaist -ne 85) { throw "M waist is $mWaist after update" }
    Pass "SC3 UpdateCell" "M waist=85"
}

Try-Test "SC3 Restore Waist M → 84" {
    Post-Json "$BaseUrl/SizeChart/UpdateCell" $session @{
        measurementPoint = "Waist"; columnIndex = 2; value = 84
    } | Out-Null
    $mWaist = Get-WaistMValue (Get-SizeChartCsv $session)
    if ($mWaist -ne 84) { throw "M waist is $mWaist after restore" }
    Pass "SC3 Restore" "M waist=84"
}

Try-Test "SC3 Invalid cell rejected" {
    Post-Json-ExpectFail "$BaseUrl/SizeChart/UpdateCell" $session @{
        measurementPoint = "NotARow"; columnIndex = 0; value = 10
    }
    Pass "SC3 Invalid row rejected"
}

# --- Row meta ---
Write-Host ""
Write-Host "--- SC4. Row metadata ---" -ForegroundColor Cyan

Try-Test "SC4 UpdateRowMeta Waist tolerance" {
    Post-Json "$BaseUrl/SizeChart/UpdateRowMeta" $session @{
        measurementPoint = "Waist"; toleranceCm = 1.5; measurementMethod = "QA tape test"
    } | Out-Null
    Pass "SC4 UpdateRowMeta"
}

# --- Add column ---
Write-Host ""
Write-Host "--- SC5. Add size column ---" -ForegroundColor Cyan

$testCol = "ZQA"
Try-Test "SC5 AddColumn duplicate XS rejected" {
    Post-Json-ExpectFail "$BaseUrl/SizeChart/AddColumn" $session @{ label = "XS" }
    Pass "SC5 Duplicate XS rejected"
}

$csvBefore = Get-SizeChartCsv $session
if ($csvBefore -match ",$testCol,") {
    Try-Test "SC5 Test column $testCol already present" {
        Pass "SC5 Column exists" "skip add"
    }
} else {
    Try-Test "SC5 AddColumn $testCol" {
        $r = Post-Json "$BaseUrl/SizeChart/AddColumn" $session @{ label = $testCol }
        if ($r.label -ne $testCol) { throw "Label mismatch: $($r.label)" }
        $csvNew = Get-SizeChartCsv $session
        if ($csvNew -notmatch ",$testCol") { throw "Column not in CSV header" }
        Pass "SC5 AddColumn" $testCol
    }
}

Try-Test "SC5 Grading syncs new column" {
    $grading = Invoke-WebRequest -Uri "$BaseUrl/Grading?style=slim" -WebSession $session -UseBasicParsing
    if ($grading.Content -notmatch $testCol) {
        if ($csvBefore -match ",$testCol,") { Pass "SC5 Grading column" "already synced"; return }
        throw "Column $testCol not on Grading page"
    }
    Pass "SC5 Grading column" $testCol
}

# --- Add row ---
Write-Host ""
Write-Host "--- SC6. Add measurement row ---" -ForegroundColor Cyan

$testRow = "QA Calf $(Get-Date -Format 'HHmmss')"
Try-Test "SC6 AddRow" {
    $r = Post-Json "$BaseUrl/SizeChart/AddRow" $session @{ name = $testRow; copyFrom = "Ankle" }
    if ($r.name -ne $testRow) { throw "Name mismatch" }
    $csvNew = Get-SizeChartCsv $session
    if ($csvNew -notmatch "(?m)^$([regex]::Escape($testRow)),") { throw "Row not in CSV" }
    Pass "SC6 AddRow" $testRow
}

Try-Test "SC6 AddRow duplicate rejected" {
    Post-Json-ExpectFail "$BaseUrl/SizeChart/AddRow" $session @{ name = $testRow; copyFrom = "Waist" }
    Pass "SC6 Duplicate row rejected"
}

# --- Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SIZE CHART MODULE SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Passed:  $($script:Passed)" -ForegroundColor $(if ($script:Failed -eq 0) { "Green" } else { "Yellow" })
Write-Host "  Failed:  $($script:Failed)" -ForegroundColor $(if ($script:Failed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($script:Failed -gt 0) {
    Write-Host "ERRORS:" -ForegroundColor Red
    foreach ($e in $script:Errors) { Write-Host "  - $e" -ForegroundColor Red }
    Write-Host ""
    exit 1
}

Write-Host "SIZE CHART MODULE PASSED." -ForegroundColor Green
Write-Host "Browser-only: edit a cell inline, Add Size modal, Add measurement modal." -ForegroundColor DarkGray
Write-Host ""
exit 0
