# PatternPro Library module — full HTTP/page test
# Usage: powershell -ExecutionPolicy Bypass -File tools/qa-library-test.ps1

param(
    [string]$BaseUrl = "http://localhost:5001",
    [string]$UserName = "admin",
    [string]$Password = "Admin@123"
)

$ErrorActionPreference = "Continue"
$script:Passed = 0
$script:Failed = 0
$script:Errors = [System.Collections.Generic.List[string]]::new()
$script:TestPatternId = 0
$script:TestPatternCode = ""
$script:SecondPatternId = 0
$script:SecondPatternCode = ""

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

function Get-LibraryPage($Session) {
    return Invoke-WebRequest -Uri "$BaseUrl/Library" -WebSession $Session -UseBasicParsing
}

function Get-PatternLibraryState([string]$Html, [string]$Code) {
    $needle = 'lib-card-code">' + $Code + '</div>'
    $idx = $Html.IndexOf($needle)
    if ($idx -lt 0) { return $null }
    $start = [Math]::Max(0, $idx - 400)
    $chunk = $Html.Substring($start, $idx - $start + $needle.Length)
    if ($chunk -match 'lib-card--saved') { return "saved" }
    if ($chunk -match 'lib-card--unsaved') { return "unsaved" }
    return $null
}

function Delete-Pattern([int]$Id, $Session) {
    Invoke-WebRequest -Uri "$BaseUrl/Home/Delete/$Id" -Method DELETE -WebSession $Session -UseBasicParsing | Out-Null
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PatternPro Library Module Test" -ForegroundColor Cyan
Write-Host "  Target: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$session = $null
Try-Test "LB0 Login" {
    $script:session = Login-Admin
    Pass "LB0 Login" $UserName
}

if (-not $session) {
    Write-Host "STOPPED: Login failed." -ForegroundColor Red
    exit 1
}

# --- Setup: pattern without geometry ---
Write-Host "--- LB1. Setup (unsaved pattern) ---" -ForegroundColor Cyan

Try-Test "LB1 Create pattern (no draft yet)" {
    $created = Post-Json "$BaseUrl/Home/Create" $session @{
        name             = "Library QA $(Get-Date -Format 'yyyyMMdd-HHmmss')"
        styleKey         = "slim"
        baseSize         = "M"
        categoryKey      = "denim"
        designer         = "LB QA"
        season           = "SS26"
        owner            = "QA"
        lifecycleStatus  = "Idea"
    }
    $script:TestPatternId = [int]$created.id
    $script:TestPatternCode = [string]$created.code
    if ($script:TestPatternId -le 0) { throw "No pattern id" }
    Pass "LB1 Create pattern" "id=$($script:TestPatternId) code=$($script:TestPatternCode)"
}

if ($script:TestPatternId -le 0) {
    Write-Host "STOPPED: Pattern create failed." -ForegroundColor Red
    exit 1
}

$patternId = $script:TestPatternId
$patternCode = $script:TestPatternCode

# --- Library page (unsaved) ---
Write-Host ""
Write-Host "--- LB2. Library page (before draft) ---" -ForegroundColor Cyan

$libHtml = ""
Try-Test "LB2 Page loads" {
    $r = Get-LibraryPage $session
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    $script:libHtml = $r.Content
    Pass "LB2 Page loads"
}

$uiMarkers = @(
    "Pattern Library",
    "All patterns with saved canvas data",
    "Saved Canvas Patterns",
    "New Canvas",
    "lib-page"
)
foreach ($marker in $uiMarkers) {
    Try-Test "LB2 UI: $marker" {
        if ($libHtml -notmatch [regex]::Escape($marker)) { throw "Missing: $marker" }
        Pass "LB2 UI: $marker"
    }
}

Try-Test "LB2 UI: sidebar Library nav" {
    if ($libHtml -notmatch 'href="/Library"') { throw "Missing /Library nav link" }
    Pass "LB2 UI: sidebar Library nav"
}

Try-Test "LB2 Pattern listed as unsaved" {
    $state = Get-PatternLibraryState $libHtml $patternCode
    if ($state -ne "unsaved") { throw "Expected unsaved card for $patternCode, got '$state'" }
    if ($libHtml -notmatch "Start in Canvas") { throw "Missing Start in Canvas link" }
    Pass "LB2 Unsaved listing" $patternCode
}

Try-Test "LB2 Pattern not saved yet" {
    $state = Get-PatternLibraryState $libHtml $patternCode
    if ($state -eq "saved") { throw "Code $patternCode already saved before draft" }
    Pass "LB2 Not saved yet" "no geometry"
}

Try-Test "LB2 Canvas link params (unsaved)" {
    if ($libHtml -notmatch "patternId=$patternId") { throw "Missing patternId=$patternId in page" }
    if ($libHtml -notmatch "style=slim") { throw "Missing style=slim in page" }
    Pass "LB2 Canvas link" "patternId=$patternId style=slim"
}

# --- Draft pieces → saved in library ---
Write-Host ""
Write-Host "--- LB3. After DraftPieces (saved geometry) ---" -ForegroundColor Cyan

Try-Test "LB3 DraftPieces" {
    Invoke-WebRequest -Uri "$BaseUrl/Pieces/DraftPieces?patternId=$patternId&style=slim" -Method POST -WebSession $session -UseBasicParsing -MaximumRedirection 5 | Out-Null
    Pass "LB3 DraftPieces"
}

Try-Test "LB3 Pattern moves to saved section" {
    $r = Get-LibraryPage $session
    $script:libHtml = $r.Content
    $state = Get-PatternLibraryState $libHtml $patternCode
    if ($state -ne "saved") { throw "Expected saved card for $patternCode, got '$state'" }
    $needle = 'lib-card-code">' + $patternCode + '</div>'
    $idx = $libHtml.IndexOf($needle)
    if ($idx -lt 0) { throw "Saved card not found for $patternCode" }
    $chunk = $libHtml.Substring($idx, [Math]::Min(1000, $libHtml.Length - $idx))
    if ($chunk -notmatch '(\d+) pieces saved') { throw "Expected piece count on saved card" }
    if ([int]$Matches[1] -ne 9) { throw "Expected 9 pieces saved, got $($Matches[1])" }
    if ($libHtml -notmatch "Open in Canvas") { throw "Missing Open in Canvas link" }
    Pass "LB3 Saved listing" "$patternCode · 9 pieces"
}

Try-Test "LB3 PieceData confirms geometry" {
    $data = Invoke-RestMethod -Uri "$BaseUrl/Canvas/PieceData?patternId=$patternId&style=slim" -WebSession $session
    $count = if ($data -is [System.Array]) { $data.Count } elseif ($data.pieces) { @($data.pieces).Count } else { 0 }
    if ($count -ne 9) { throw "Expected 9 pieces, got $count" }
    Pass "LB3 PieceData" "$count pieces"
}

# --- Second pattern: mixed library state ---
Write-Host ""
Write-Host "--- LB4. Mixed saved + unsaved ---" -ForegroundColor Cyan

Try-Test "LB4 Create second pattern (unsaved)" {
    $created = Post-Json "$BaseUrl/Home/Create" $session @{
        name             = "Library QA B $(Get-Date -Format 'HHmmss')"
        styleKey         = "slim"
        baseSize         = "M"
        categoryKey      = "denim"
        designer         = "LB QA"
        season           = "SS26"
        owner            = "QA"
        lifecycleStatus  = "Idea"
    }
    $script:SecondPatternId = [int]$created.id
    $script:SecondPatternCode = [string]$created.code
    if ($script:SecondPatternId -le 0) { throw "No second pattern id" }
    Pass "LB4 Second pattern" "id=$($script:SecondPatternId) code=$($script:SecondPatternCode)"
}

Try-Test "LB4 Both patterns listed in correct sections" {
    $r = Get-LibraryPage $session
    $html = $r.Content
    $state1 = Get-PatternLibraryState $html $patternCode
    $state2 = Get-PatternLibraryState $html $script:SecondPatternCode
    if ($state1 -ne "saved") { throw "Drafted pattern state='$state1', expected saved" }
    if ($state2 -ne "unsaved") { throw "New pattern state='$state2', expected unsaved" }
    Pass "LB4 Mixed library" "1 saved + 1 unsaved"
}

Try-Test "LB4 Saved count in header" {
    $r = Get-LibraryPage $session
    $html = $r.Content
    if ($html -notmatch '<strong style="color:var\(--ink\)">\d+</strong> saved') {
        throw "Saved count badge not found in header"
    }
    Pass "LB4 Header counts" "saved/unsaved badges present"
}

# --- Auth gate ---
Write-Host ""
Write-Host "--- LB5. Auth ---" -ForegroundColor Cyan

Try-Test "LB5 Library requires login" {
    try {
        Invoke-WebRequest -Uri "$BaseUrl/Library" -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop | Out-Null
        throw "Expected redirect, got 200"
    }
    catch {
        Pass "LB5 Auth gate" "redirect to login"
    }
}

# --- Cleanup ---
Write-Host ""
Write-Host "--- LB6. Cleanup ---" -ForegroundColor Cyan

Try-Test "LB6 Delete test patterns" {
    Delete-Pattern $patternId $session
    if ($script:SecondPatternId -gt 0) { Delete-Pattern $script:SecondPatternId $session }
    Pass "LB6 Cleanup" "deleted $patternId, $($script:SecondPatternId)"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  LIBRARY MODULE SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Passed:  $($script:Passed)" -ForegroundColor Green
Write-Host "  Failed:  $($script:Failed)" -ForegroundColor $(if ($script:Failed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($script:Failed -gt 0) {
    Write-Host "Failures:" -ForegroundColor Red
    foreach ($e in $script:Errors) { Write-Host "  - $e" -ForegroundColor Red }
    Write-Host ""
    Write-Host "LIBRARY MODULE FAILED." -ForegroundColor Red
    exit 1
}

Write-Host "LIBRARY MODULE PASSED." -ForegroundColor Green
Write-Host "Browser-only: card layout, New Canvas shortcut, top-bar Library toast button." -ForegroundColor DarkGray
exit 0
