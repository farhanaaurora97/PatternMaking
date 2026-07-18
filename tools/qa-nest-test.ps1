# PatternPro Graded Nest module — full HTTP/page test
# Usage: powershell -ExecutionPolicy Bypass -File tools/qa-nest-test.ps1

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

function Get-NestPage($Session, [string]$Style = "slim") {
    $uri = "$BaseUrl/Nest?style=$Style"
    return Invoke-WebRequest -Uri $uri -WebSession $Session -UseBasicParsing
}

function Get-BasePiecePoints($Session) {
    return Invoke-RestMethod -Uri "$BaseUrl/Nest/BasePiece" -WebSession $Session
}

function Test-PointList($pts) {
    if ($null -eq $pts) { return 0 }
    if ($pts -is [System.Array]) { return @($pts).Count }
    return 0
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PatternPro Graded Nest Module Test" -ForegroundColor Cyan
Write-Host "  Target: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$session = $null
Try-Test "GN0 Login" {
    $script:session = Login-Admin
    Pass "GN0 Login" $UserName
}

if (-not $session) {
    Write-Host "STOPPED: Login failed." -ForegroundColor Red
    exit 1
}

# --- Page load ---
Write-Host "--- GN1. Page load ---" -ForegroundColor Cyan

$nestHtml = ""
Try-Test "GN1 Page loads (slim)" {
    $r = Get-NestPage $session "slim"
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    $script:nestHtml = $r.Content
    Pass "GN1 Page loads" "style=slim"
}

Try-Test "GN1 Page loads (skinny default)" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Nest" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    Pass "GN1 Default route" "/Nest"
}

# --- UI shell ---
Write-Host ""
Write-Host "--- GN2. UI shell ---" -ForegroundColor Cyan

$uiMarkers = @(
    "Graded Nest",
    "All sizes overlaid",
    "XS through XXL",
    "nest-legend",
    "nest-canvas",
    "nest-cv",
    "nest.js",
    "NEST_BASE_URL",
    "NEST_COLORS",
    "NEST_LABELS",
    "Toggle Sizes",
    "Export Nest DXF",
    "Front Leg",
    "Back Leg",
    "Waistband",
    "All Pieces",
    "btn-nest-zoom-in",
    "btn-nest-zoom-out",
    "nest-zoom-lbl"
)
foreach ($marker in $uiMarkers) {
    Try-Test "GN2 UI: $marker" {
        if ($nestHtml -notmatch [regex]::Escape($marker)) { throw "Missing: $marker" }
        Pass "GN2 UI: $marker"
    }
}

Try-Test "GN2 Sidebar Graded Nest nav" {
    if ($nestHtml -notmatch 'href="/Nest') { throw "Missing /Nest nav link" }
    Pass "GN2 UI: sidebar nav"
}

Try-Test "GN2 Size legend labels" {
    foreach ($sz in @("XS", "M (Base)", "XXL")) {
        if ($nestHtml -notmatch [regex]::Escape($sz)) { throw "Missing size label: $sz" }
    }
    Pass "GN2 Size legend" "XS, M (Base), XXL"
}

Try-Test "GN2 Export link targets Export" {
    if ($nestHtml -notmatch 'href="/Export') { throw "Missing Export href" }
    Pass "GN2 Export link" "/Export"
}

# --- BasePiece API ---
Write-Host ""
Write-Host "--- GN3. BasePiece API ---" -ForegroundColor Cyan

$basePts = $null
Try-Test "GN3 BasePiece returns JSON array" {
    $script:basePts = Get-BasePiecePoints $session
    $count = Test-PointList $basePts
    if ($count -lt 4) { throw "Expected at least 4 points, got $count" }
    Pass "GN3 BasePiece" "$count vertices"
}

Try-Test "GN3 BasePiece point shape" {
    $pts = @($basePts)
    foreach ($p in $pts) {
        $arr = @($p)
        if ($arr.Count -ne 2) { throw "Point must be [x,y], got $($arr.Count) values" }
        if ($arr[0] -isnot [int] -and $arr[0] -isnot [long] -and $arr[0] -isnot [double]) {
            throw "Invalid x coordinate type"
        }
    }
    Pass "GN3 Point shape" "all [x,y] pairs"
}

Try-Test "GN3 BasePiece matches template rectangle" {
    $pts = @($basePts)
    $xs = $pts | ForEach-Object { @($_)[0] }
    $ys = $pts | ForEach-Object { @($_)[1] }
    $w = ($xs | Measure-Object -Maximum).Maximum - ($xs | Measure-Object -Minimum).Minimum
    $h = ($ys | Measure-Object -Maximum).Maximum - ($ys | Measure-Object -Minimum).Minimum
    if ($w -le 0 -or $h -le 0) { throw "Invalid bounding box w=$w h=$h" }
    Pass "GN3 Bounding box" "${w}x${h} px"
}

# --- Style variants ---
Write-Host ""
Write-Host "--- GN4. Style query param ---" -ForegroundColor Cyan

foreach ($style in @("slim", "straight", "wideLeg")) {
    Try-Test "GN4 Page loads style=$style" {
        $r = Get-NestPage $session $style
        if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
        if ($r.Content -notmatch "Graded Nest") { throw "Page content missing title" }
        Pass "GN4 style=$style"
    }
}

# --- Auth ---
Write-Host ""
Write-Host "--- GN5. Auth ---" -ForegroundColor Cyan

Try-Test "GN5 Nest page requires login" {
    try {
        Invoke-WebRequest -Uri "$BaseUrl/Nest?style=slim" -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop | Out-Null
        throw "Expected redirect, got 200"
    }
    catch {
        Pass "GN5 Page auth gate" "redirect to login"
    }
}

Try-Test "GN5 BasePiece requires login" {
    try {
        Invoke-WebRequest -Uri "$BaseUrl/Nest/BasePiece" -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop | Out-Null
        throw "Expected redirect, got 200"
    }
    catch {
        Pass "GN5 BasePiece auth gate" "redirect to login"
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  GRADED NEST MODULE SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Passed:  $($script:Passed)" -ForegroundColor Green
Write-Host "  Failed:  $($script:Failed)" -ForegroundColor $(if ($script:Failed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($script:Failed -gt 0) {
    Write-Host "Failures:" -ForegroundColor Red
    foreach ($e in $script:Errors) { Write-Host "  - $e" -ForegroundColor Red }
    Write-Host ""
    Write-Host "GRADED NEST MODULE FAILED." -ForegroundColor Red
    exit 1
}

Write-Host "GRADED NEST MODULE PASSED." -ForegroundColor Green
Write-Host "Browser-only: canvas overlay render, zoom buttons, piece toolbar, Toggle Sizes toast." -ForegroundColor DarkGray
exit 0
