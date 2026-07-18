# PatternPro Block Generator module — full HTTP/API test
# Usage: powershell -ExecutionPolicy Bypass -File tools/qa-block-generator-test.ps1

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

function Get-PieceList($data) {
    if ($null -eq $data) { return @() }
    if ($data -is [System.Array]) { return @($data) }
    return @($data)
}

function Test-EaseOnPage([string]$Html, [string]$Key, [string]$CmValue) {
    # Razor HTML-encodes '+' as &#x2B; in rendered text
    $patterns = @(
        "data-ease-key=`"$Key`"[^>]*>\+$CmValue cm",
        "data-ease-key=`"$Key`"[^>]*>&#x2B;$CmValue cm"
    )
    foreach ($p in $patterns) {
        if ($Html -match $p) { return $true }
    }
    return $false
}

function Delete-Pattern([string]$Uri, $Session) {
    Invoke-WebRequest -Uri $Uri -Method DELETE -WebSession $Session -UseBasicParsing | Out-Null
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PatternPro Block Generator Module Test" -ForegroundColor Cyan
Write-Host "  Target: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$session = $null
Try-Test "BG0 Login" {
    $script:session = Login-Admin
    Pass "BG0 Login" $UserName
}

if (-not $session) {
    Write-Host "STOPPED: Login failed." -ForegroundColor Red
    exit 1
}

# --- Page shell (slim) ---
Write-Host "--- BG1. Block Generator page ---" -ForegroundColor Cyan

$pageHtml = ""
Try-Test "BG1 Page loads (slim)" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/BlockGenerator?style=slim" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    $script:pageHtml = $r.Content
    Pass "BG1 Page loads" "slim"
}

$uiMarkers = @(
    @{ Name = "ease list";          Pattern = "ease-list" },
    @{ Name = "fit profile";        Pattern = "fit-profile" },
    @{ Name = "formula list";       Pattern = "formula-list" },
    @{ Name = "reset ease button";  Pattern = "btn-reset-ease" },
    @{ Name = "generate block btn"; Pattern = "btn-generate-block" },
    @{ Name = "blockgen.js";        Pattern = "blockgen.js" },
    @{ Name = "Slim Fit label";     Pattern = "Slim Fit" }
)
foreach ($m in $uiMarkers) {
    Try-Test "BG1 UI: $($m.Name)" {
        if ($pageHtml -notmatch [regex]::Escape($m.Pattern)) { throw "Missing: $($m.Pattern)" }
        Pass "BG1 UI: $($m.Name)"
    }
}

Try-Test "BG1 Default slim Thigh ease" {
    if (-not (Test-EaseOnPage $pageHtml "Thigh" "2")) { throw "Thigh +2 cm not found (may be overridden)" }
    Pass "BG1 Thigh ease" "+2 cm default"
}

Try-Test "BG1 Drafting formulas present" {
    foreach ($f in @("Waist/4", "Hip/4", "Inseam")) {
        if ($pageHtml -notmatch [regex]::Escape($f)) { throw "Missing formula: $f" }
    }
    Pass "BG1 Formulas" "Waist/4, Hip/4, Inseam"
}

# --- All fit styles ---
Write-Host ""
Write-Host "--- BG2. All fit profiles ---" -ForegroundColor Cyan

$styles = @(
    @{ Key = "skinny";   Label = "Skinny Fit";   Snippet = "Ultra-close" },
    @{ Key = "slim";     Label = "Slim Fit";     Snippet = "Tapered" },
    @{ Key = "straight"; Label = "Straight Fit"; Snippet = "straight cut" },
    @{ Key = "bootcut";  Label = "Bootcut Fit";  Snippet = "flared" },
    @{ Key = "wideLeg";  Label = "Wide Leg Fit"; Snippet = "Relaxed" }
)
foreach ($s in $styles) {
    Try-Test "BG2 Style $($s.Key)" {
        $r = Invoke-WebRequest -Uri "$BaseUrl/BlockGenerator?style=$($s.Key)" -WebSession $session -UseBasicParsing
        if ($r.Content -notmatch [regex]::Escape($s.Label)) { throw "Label $($s.Label) missing" }
        if ($r.Content -notmatch $s.Snippet) { throw "Fit profile snippet missing" }
        Pass "BG2 $($s.Key)" $s.Label
    }
}

# --- Ease save / reset ---
Write-Host ""
Write-Host "--- BG3. Ease overrides ---" -ForegroundColor Cyan

Try-Test "BG3 SaveEase Thigh slim -> 3" {
    $ease = Post-Json "$BaseUrl/BlockGenerator/SaveEase" $session @{
        styleKey = "slim"; measurementKey = "Thigh"; value = 3
    }
    if ([decimal]$ease.Thigh -ne 3) { throw "Thigh=$($ease.Thigh)" }
    Pass "BG3 SaveEase" "Thigh=3"
}

Try-Test "BG3 SaveEase persists on page reload" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/BlockGenerator?style=slim" -WebSession $session -UseBasicParsing
    if (-not (Test-EaseOnPage $r.Content "Thigh" "3")) { throw "Thigh +3 cm not on page" }
    Pass "BG3 Persisted" "reload shows +3 cm"
}

Try-Test "BG3 ResetEase slim" {
    $ease = Invoke-RestMethod -Uri "$BaseUrl/BlockGenerator/ResetEase?styleKey=slim" -Method POST -WebSession $session
    if ([decimal]$ease.Thigh -ne 2) { throw "Thigh after reset=$($ease.Thigh)" }
    Pass "BG3 ResetEase" "Thigh=2"
}

# --- Generate block API ---
Write-Host ""
Write-Host "--- BG4. Generate block ---" -ForegroundColor Cyan

Try-Test "BG4 GenerateBlock slim" {
    $r = Invoke-RestMethod -Uri "$BaseUrl/BlockGenerator/GenerateBlock?styleKey=slim" -Method POST -WebSession $session
    if ($r.styleLabel -ne "Slim Fit") { throw "styleLabel=$($r.styleLabel)" }
    if ([int]$r.pieceCount -lt 6) { throw "pieceCount=$($r.pieceCount)" }
    Pass "BG4 GenerateBlock" "$($r.styleLabel), $($r.pieceCount) formulas"
}

# --- Draft pieces for pattern ---
Write-Host ""
Write-Host "--- BG5. Draft pieces from block ---" -ForegroundColor Cyan

Try-Test "BG5 Create test pattern" {
    $created = Post-Json "$BaseUrl/Home/Create" $session @{
        name = "BlockGen QA $(Get-Date -Format 'yyyyMMdd-HHmmss')"
        styleKey = "slim"; baseSize = "M"; categoryKey = "denim"
        designer = "BG QA"; season = "SS26"; owner = "QA"; lifecycleStatus = "Idea"
    }
    $script:TestPatternId = [int]$created.id
    if ($script:TestPatternId -le 0) { throw "No pattern id" }
    Pass "BG5 Create pattern" "id=$($script:TestPatternId)"
}

if ($script:TestPatternId -le 0) {
    Write-Host "STOPPED: Pattern create failed." -ForegroundColor Red
    exit 1
}
$patternId = $script:TestPatternId

Try-Test "BG5 DraftPieces (Generate Pattern)" {
    Invoke-WebRequest -Uri "$BaseUrl/Pieces/DraftPieces?patternId=$patternId&style=slim" -Method POST -WebSession $session -UseBasicParsing | Out-Null
    Pass "BG5 DraftPieces"
}

Try-Test "BG5 Pieces page loads" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Pieces?patternId=$patternId&style=slim" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    Pass "BG5 Pieces page"
}

Try-Test "BG5 Canvas has required pieces" {
    $data = Invoke-RestMethod -Uri "$BaseUrl/Canvas/PieceData?patternId=$patternId&style=slim" -WebSession $session
    $pieces = Get-PieceList $data
    $names = @($pieces | ForEach-Object { if ($_.name) { $_.name } else { $_.Name } })
    foreach ($req in @("Front Leg", "Back Leg", "Waistband")) {
        if ($names -notcontains $req) { throw "Missing: $req (found: $($names -join ', '))" }
    }
    Pass "BG5 Required pieces" "$($names.Count) pieces"
}

Try-Test "BG5 Pattern piece count updated" {
    $rows = Invoke-RestMethod -Uri "$BaseUrl/Home/Patterns" -WebSession $session
    $p = @($rows) | Where-Object { $_.id -eq $patternId } | Select-Object -First 1
    if (-not $p) { throw "Pattern not in list" }
    if ([int]$p.pieceCount -lt 6) { throw "pieceCount=$($p.pieceCount)" }
    Pass "BG5 Piece count" "$($p.pieceCount) on dashboard row"
}

# --- Cleanup ---
Write-Host ""
Write-Host "--- BG6. Cleanup ---" -ForegroundColor Cyan

Try-Test "BG6 Delete test pattern" {
    Delete-Pattern "$BaseUrl/Home/Delete/$patternId" $session
    Pass "BG6 Delete test pattern" "id=$patternId"
}

# --- Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  BLOCK GENERATOR MODULE SUMMARY" -ForegroundColor Cyan
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

Write-Host "BLOCK GENERATOR MODULE PASSED." -ForegroundColor Green
Write-Host "Browser-only: click ease value to edit inline, Generate Block toast." -ForegroundColor DarkGray
Write-Host ""
exit 0
