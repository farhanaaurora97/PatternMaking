# PatternPro Grading module — full HTTP/API test
# Usage: powershell -ExecutionPolicy Bypass -File tools/qa-grading-test.ps1

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

function Post-Form([string]$Uri, $Session, [string]$Body) {
    return Invoke-RestMethod -Uri $Uri -Method POST -Body $Body -ContentType "application/x-www-form-urlencoded" -WebSession $Session
}

function Post-Form-ExpectFail([string]$Uri, $Session, [string]$Body) {
    try {
        Invoke-RestMethod -Uri $Uri -Method POST -Body $Body -ContentType "application/x-www-form-urlencoded" -WebSession $Session -ErrorAction Stop | Out-Null
        throw "Expected HTTP error but request succeeded"
    }
    catch {
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -ge 400) { return }
        throw
    }
}

function Get-GradingCsv($Session, [string]$Style = "slim") {
    return (Invoke-WebRequest -Uri "$BaseUrl/Grading/ExportCsv?style=$Style" -WebSession $Session -UseBasicParsing).Content
}

function Get-WaistLDelta([string]$Csv) {
    $waistLine = ($Csv -split "`n") | Where-Object { $_ -match "^Waist," } | Select-Object -First 1
    if (-not $waistLine) { throw "Waist row not found" }
    $parts = $waistLine -split ","
    if ($parts.Count -lt 5) { throw "Waist row too short: $waistLine" }
    $raw = $parts[4].Trim().TrimStart('+')
    return [double]$raw
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PatternPro Grading Module Test" -ForegroundColor Cyan
Write-Host "  Target: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$session = $null
Try-Test "GR0 Login" {
    $script:session = Login-Admin
    Pass "GR0 Login" $UserName
}

if (-not $session) {
    Write-Host "STOPPED: Login failed." -ForegroundColor Red
    exit 1
}

# --- Page ---
Write-Host "--- GR1. Grading page ---" -ForegroundColor Cyan

$pageHtml = ""
Try-Test "GR1 Page loads (slim)" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Grading?style=slim" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    $script:pageHtml = $r.Content
    Pass "GR1 Page loads" "slim"
}

$uiMarkers = @(
    @{ Name = "fit tabs";           Pattern = "fit-tabs" },
    @{ Name = "add size button";    Pattern = "btn-add-col" },
    @{ Name = "add row button";     Pattern = "btn-add-row" },
    @{ Name = "export link";        Pattern = "ExportCsv" },
    @{ Name = "M base column";      Pattern = "sch-m" },
    @{ Name = "editable deltas";    Pattern = "gr-delta-input" },
    @{ Name = "Waist row";          Pattern = "Waist" },
    @{ Name = "Slim Fit title";     Pattern = "Slim Fit" }
)
foreach ($m in $uiMarkers) {
    Try-Test "GR1 UI: $($m.Name)" {
        if ($pageHtml -notmatch [regex]::Escape($m.Pattern)) { throw "Missing: $($m.Pattern)" }
        Pass "GR1 UI: $($m.Name)"
    }
}

foreach ($size in @("XS", "S", "M", "L", "XL", "XXL")) {
    Try-Test "GR1 Column $size" {
        if ($pageHtml -notmatch ">$size<") { throw "Column $size not in header" }
        Pass "GR1 Column $size"
    }
}

# --- All fits ---
Write-Host ""
Write-Host "--- GR2. All fit profiles ---" -ForegroundColor Cyan

$styles = @(
    @{ Key = "skinny";   Label = "Skinny Fit" },
    @{ Key = "slim";     Label = "Slim Fit" },
    @{ Key = "straight"; Label = "Straight Fit" },
    @{ Key = "bootcut";  Label = "Bootcut Fit" },
    @{ Key = "wideLeg";  Label = "Wide Leg Fit" }
)
foreach ($s in $styles) {
    Try-Test "GR2 Style $($s.Key)" {
        $r = Invoke-WebRequest -Uri "$BaseUrl/Grading?style=$($s.Key)" -WebSession $session -UseBasicParsing
        if ($r.Content -notmatch [regex]::Escape($s.Label)) { throw "Missing $($s.Label)" }
        if ($r.Content -notmatch "Grade Rules") { throw "Missing table title" }
        Pass "GR2 $($s.Key)" $s.Label
    }
}

# --- CSV export ---
Write-Host ""
Write-Host "--- GR3. Export CSV ---" -ForegroundColor Cyan

$csv = ""
Try-Test "GR3 CSV export slim" {
    $script:csv = Get-GradingCsv $session "slim"
    if ($csv.Length -lt 50) { throw "CSV too short" }
    if ($csv -notmatch "^Measurement,") { throw "Missing header" }
    if ($csv -notmatch "M\(Base\)") { throw "M(Base) column missing" }
    Pass "GR3 CSV export" "$($csv.Split("`n").Count) lines"
}

Try-Test "GR3 Default Waist L delta = +2" {
    $l = Get-WaistLDelta $csv
    if ($l -ne 2) { throw "Waist L delta is $l, expected 2" }
    Pass "GR3 Waist L" "+2 cm"
}

Try-Test "GR3 Default measurement rows" {
    foreach ($pom in @("Waist", "Hip", "Thigh", "Inseam")) {
        if ($csv -notmatch "(?m)^$([regex]::Escape($pom)),") { throw "Missing: $pom" }
    }
    Pass "GR3 Rows" "Waist, Hip, Thigh, Inseam"
}

# --- Update delta ---
Write-Host ""
Write-Host "--- GR4. Update delta ---" -ForegroundColor Cyan

Try-Test "GR4 UpdateDelta Waist L -> 3" {
    Post-Json "$BaseUrl/Grading/UpdateDelta" $session @{
        styleKey = "slim"; measurementPoint = "Waist"; columnIndex = 3; delta = 3
    } | Out-Null
    $l = Get-WaistLDelta (Get-GradingCsv $session "slim")
    if ($l -ne 3) { throw "Waist L is $l after update" }
    Pass "GR4 UpdateDelta" "L=+3"
}

Try-Test "GR4 Restore Waist L -> 2" {
    Post-Json "$BaseUrl/Grading/UpdateDelta" $session @{
        styleKey = "slim"; measurementPoint = "Waist"; columnIndex = 3; delta = 2
    } | Out-Null
    $l = Get-WaistLDelta (Get-GradingCsv $session "slim")
    if ($l -ne 2) { throw "Waist L is $l after restore" }
    Pass "GR4 Restore" "L=+2"
}

Try-Test "GR4 Base column edit rejected" {
    Post-Json-ExpectFail "$BaseUrl/Grading/UpdateDelta" $session @{
        styleKey = "slim"; measurementPoint = "Waist"; columnIndex = 2; delta = 1
    }
    Pass "GR4 Base column blocked"
}

Try-Test "GR4 Invalid row rejected" {
    Post-Json-ExpectFail "$BaseUrl/Grading/UpdateDelta" $session @{
        styleKey = "slim"; measurementPoint = "NotARow"; columnIndex = 3; delta = 1
    }
    Pass "GR4 Invalid row rejected"
}

# --- Add column ---
Write-Host ""
Write-Host "--- GR5. Add size column ---" -ForegroundColor Cyan

$testCol = "GQAG"
$csvBefore = Get-GradingCsv $session "slim"

Try-Test "GR5 Duplicate XS rejected" {
    Post-Form-ExpectFail "$BaseUrl/Grading/AddColumn" $session "label=XS"
    Pass "GR5 Duplicate XS rejected"
}

if ($csvBefore -match ",$testCol") {
    Try-Test "GR5 Column $testCol exists" { Pass "GR5 Column" "skip add" }
} else {
    Try-Test "GR5 AddColumn $testCol" {
        $r = Post-Form "$BaseUrl/Grading/AddColumn" $session "label=$testCol"
        if ($r.label -ne $testCol) { throw "Label=$($r.label)" }
        $csvNew = Get-GradingCsv $session "slim"
        if ($csvNew -notmatch ",$testCol") { throw "Column not in CSV header" }
        Pass "GR5 AddColumn" $testCol
    }
}

# --- Add row ---
Write-Host ""
Write-Host "--- GR6. Add measurement row ---" -ForegroundColor Cyan

$testRow = "QA Grade $(Get-Date -Format 'HHmmss')"
Try-Test "GR6 AddRow copy from Waist" {
    $body = "style=slim&measurementPoint=$([uri]::EscapeDataString($testRow))&copyFrom=Waist"
    $r = Post-Form "$BaseUrl/Grading/AddRow" $session $body
    if ($r.measurementPoint -ne $testRow) { throw "Name mismatch" }
    $csvNew = Get-GradingCsv $session "slim"
    if ($csvNew -notmatch "(?m)^$([regex]::Escape($testRow)),") { throw "Row not in CSV" }
    Pass "GR6 AddRow" $testRow
}

Try-Test "GR6 Duplicate row rejected" {
    $body = "style=slim&measurementPoint=$([uri]::EscapeDataString($testRow))&copyFrom=Waist"
    Post-Form-ExpectFail "$BaseUrl/Grading/AddRow" $session $body
    Pass "GR6 Duplicate rejected"
}

# --- Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  GRADING MODULE SUMMARY" -ForegroundColor Cyan
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

Write-Host "GRADING MODULE PASSED." -ForegroundColor Green
Write-Host "Browser-only: edit delta inline, Add Size/Row modals." -ForegroundColor DarkGray
Write-Host ""
exit 0
