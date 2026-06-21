# PatternPro Pattern Pieces module — full HTTP/API test
# Usage: powershell -ExecutionPolicy Bypass -File tools/qa-pieces-test.ps1

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
$script:TestPieceName = ""

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
    if ($null -ne $data.pieces) { return @($data.pieces) }
    return @($data)
}

function Get-PieceNames($pieces) {
    return @($pieces | ForEach-Object {
        if ($_.name) { $_.name } elseif ($_.Name) { $_.Name } else { "" }
    })
}

function Get-PieceSa($p) {
    if ($null -ne $p.sa) { return [double]$p.sa }
    if ($null -ne $p.Sa) { return [double]$p.Sa }
    if ($null -ne $p.seamAllowance) { return [double]$p.seamAllowance }
    if ($null -ne $p.SeamAllowance) { return [double]$p.SeamAllowance }
    return 0
}

function Get-PiecePts($p) {
    if ($p.pts) { return @($p.pts) }
    if ($p.Pts) { return @($p.Pts) }
    if ($p.points) { return @($p.points) }
    if ($p.Points) { return @($p.Points) }
    return @()
}

function Get-PieceData($Session, [int]$PatternId, [string]$Style) {
    return Get-PieceList (Invoke-RestMethod -Uri "$BaseUrl/Canvas/PieceData?patternId=$PatternId&style=$Style" -WebSession $Session)
}

function Get-AntiforgeryToken([string]$Html) {
    $m = [regex]::Match($Html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $m.Success) { throw "Anti-forgery token not found on page" }
    return $m.Groups[1].Value
}

function Delete-Pattern([string]$Uri, $Session) {
    Invoke-WebRequest -Uri $Uri -Method DELETE -WebSession $Session -UseBasicParsing | Out-Null
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PatternPro Pattern Pieces Module Test" -ForegroundColor Cyan
Write-Host "  Target: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$session = $null
Try-Test "PP0 Login" {
    $script:session = Login-Admin
    Pass "PP0 Login" $UserName
}

if (-not $session) {
    Write-Host "STOPPED: Login failed." -ForegroundColor Red
    exit 1
}

# --- Create pattern ---
Write-Host "--- PP1. Setup ---" -ForegroundColor Cyan

Try-Test "PP1 Create test pattern" {
    $created = Post-Json "$BaseUrl/Home/Create" $session @{
        name = "Pieces QA $(Get-Date -Format 'yyyyMMdd-HHmmss')"
        styleKey = "slim"; baseSize = "M"; categoryKey = "denim"
        designer = "PP QA"; season = "SS26"; owner = "QA"; lifecycleStatus = "Idea"
    }
    $script:TestPatternId = [int]$created.id
    if ($script:TestPatternId -le 0) { throw "No pattern id" }
    Pass "PP1 Create pattern" "id=$($script:TestPatternId) code=$($created.code)"
}

if ($script:TestPatternId -le 0) {
    Write-Host "STOPPED: Pattern create failed." -ForegroundColor Red
    exit 1
}
$patternId = $script:TestPatternId

# --- Page before draft ---
Write-Host ""
Write-Host "--- PP2. Pieces page (before draft) ---" -ForegroundColor Cyan

Try-Test "PP2 Page loads before draft" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Pieces?patternId=$patternId&style=slim" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    Pass "PP2 Page loads"
}

# --- Generate pattern (draft + auto-refine) ---
Write-Host ""
Write-Host "--- PP3. Generate Pattern ---" -ForegroundColor Cyan

$pageHtml = ""
Try-Test "PP3 DraftPieces (Generate Pattern)" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Pieces/DraftPieces?patternId=$patternId&style=slim" -Method POST -WebSession $session -UseBasicParsing -MaximumRedirection 5
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    $script:pageHtml = $r.Content
    Pass "PP3 DraftPieces"
}

$uiMarkers = @(
    @{ Name = "Generate Pattern"; Pattern = "Generate Pattern" },
    @{ Name = "Auto-refine";      Pattern = "Auto-refine" },
    @{ Name = "Add Piece";        Pattern = "btn-add-piece" },
    @{ Name = "Open Canvas";      Pattern = "Open All in Canvas" },
    @{ Name = "Fabric Cut Summary"; Pattern = "Fabric Cut Summary" },
    @{ Name = "piece grid";       Pattern = "pcs-grid" },
    @{ Name = "pieces.js";        Pattern = "pieces.js" }
)
foreach ($m in $uiMarkers) {
    Try-Test "PP3 UI: $($m.Name)" {
        if ($pageHtml -notmatch [regex]::Escape($m.Pattern)) { throw "Missing: $($m.Pattern)" }
        Pass "PP3 UI: $($m.Name)"
    }
}

Try-Test "PP3 Slim has 9 pieces on page" {
    if ($pageHtml -notmatch "9 pieces") { throw "Expected '9 pieces' in subtitle" }
    Pass "PP3 Piece count" "9 slim pieces"
}

$slimRequired = @("Front Leg", "Back Leg", "Waistband", "Fly Facing", "Coin Pocket", "Belt Loop")
foreach ($pieceLabel in $slimRequired) {
    Try-Test "PP3 Piece card: $pieceLabel" {
        if ($pageHtml -notmatch [regex]::Escape($pieceLabel)) { throw "Missing on page: $pieceLabel" }
        Pass "PP3 Piece card: $pieceLabel"
    }
}

# --- Canvas piece data ---
Write-Host ""
Write-Host "--- PP4. Piece geometry ---" -ForegroundColor Cyan

Try-Test "PP4 Canvas PieceData count" {
    $pieces = Get-PieceData $session $patternId "slim"
    if ($pieces.Count -ne 9) { throw "Expected 9 pieces, got $($pieces.Count)" }
    Pass "PP4 PieceData" "9 pieces"
}

Try-Test "PP4 Required pieces present" {
    $pieces = Get-PieceData $session $patternId "slim"
    $names = Get-PieceNames $pieces
    foreach ($req in @("Front Leg", "Back Leg", "Waistband")) {
        if ($names -notcontains $req) { throw "Missing: $req" }
    }
    Pass "PP4 Required pieces" ($names -join ", ")
}

Try-Test "PP4 Seam allowance on body panels" {
    $pieces = Get-PieceData $session $patternId "slim"
    foreach ($req in @("Front Leg", "Back Leg", "Waistband")) {
        $p = $pieces | Where-Object {
            $n = if ($_.name) { $_.name } else { $_.Name }
            $n -eq $req
        } | Select-Object -First 1
        if (-not $p) { throw "Piece not found: $req" }
        $sa = Get-PieceSa $p
        if ($sa -le 0) { throw "$req sa=$sa" }
    }
    Pass "PP4 Seam allowance" "Front/Back/Waistband > 0"
}

Try-Test "PP4 Points geometry not empty" {
    $pieces = Get-PieceData $session $patternId "slim"
    $front = $pieces | Where-Object {
        $n = if ($_.name) { $_.name } else { $_.Name }
        $n -eq "Front Leg"
    } | Select-Object -First 1
    $pts = Get-PiecePts $front
    if ($pts.Count -lt 4) { throw "Front Leg has $($pts.Count) points" }
    Pass "PP4 Geometry" "Front Leg $($pts.Count) vertices"
}

# --- Auto-refine ---
Write-Host ""
Write-Host "--- PP5. Auto-refine ---" -ForegroundColor Cyan

Try-Test "PP5 RefinePieces" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Pieces/RefinePieces?patternId=$patternId&style=slim" -Method POST -WebSession $session -UseBasicParsing -MaximumRedirection 5
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    if ($r.Content -notmatch "Auto-refine") { throw "Refine page did not load" }
    Pass "PP5 RefinePieces"
}

Try-Test "PP5 Still 9 pieces after refine" {
    $pieces = Get-PieceData $session $patternId "slim"
    if ($pieces.Count -ne 9) { throw "Count=$($pieces.Count)" }
    Pass "PP5 Piece count unchanged" "9"
}

# --- Add / delete piece ---
Write-Host ""
Write-Host "--- PP6. Add & delete piece ---" -ForegroundColor Cyan

$script:TestPieceName = "QA Knee Patch $(Get-Date -Format 'HHmmss')"
Try-Test "PP6 AddPiece" {
    $page = Invoke-WebRequest -Uri "$BaseUrl/Pieces?patternId=$patternId&style=slim" -WebSession $session -UseBasicParsing
    $token = Get-AntiforgeryToken $page.Content
    $body = @{
        patternId                   = $patternId
        style                       = "slim"
        pieceName                   = $script:TestPieceName
        category                    = "Hardware & Details"
        cut                         = "Cut 2"
        grainLine                   = "Straight"
        color                       = "#a78bfa"
        description                 = "QA test piece"
        __RequestVerificationToken  = $token
    }
    $r = Invoke-RestMethod -Uri "$BaseUrl/Pieces/AddPiece" -Method POST -Body $body -WebSession $session
    if ($r.name -ne $script:TestPieceName) { throw "Name=$($r.name)" }
    Pass "PP6 AddPiece" $script:TestPieceName
}

Try-Test "PP6 Piece count 10 after add" {
    $pieces = Get-PieceData $session $patternId "slim"
    if ($pieces.Count -ne 10) { throw "Count=$($pieces.Count)" }
    $names = Get-PieceNames $pieces
    if ($names -notcontains $script:TestPieceName) { throw "Custom piece not in list" }
    Pass "PP6 Count after add" "10 pieces"
}

Try-Test "PP6 DeletePiece" {
    $page = Invoke-WebRequest -Uri "$BaseUrl/Pieces?patternId=$patternId&style=slim" -WebSession $session -UseBasicParsing
    $token = Get-AntiforgeryToken $page.Content
    $body = @{
        patternId                  = $patternId
        style                      = "slim"
        pieceName                  = $script:TestPieceName
        __RequestVerificationToken = $token
    }
    $r = Invoke-WebRequest -Uri "$BaseUrl/Pieces/DeletePiece" -Method POST -Body $body -WebSession $session -UseBasicParsing
    if ($r.Content -match [regex]::Escape($script:TestPieceName)) { throw "Piece still on page after delete" }
    Pass "PP6 DeletePiece"
}

Try-Test "PP6 Back to 9 pieces after delete" {
    $pieces = Get-PieceData $session $patternId "slim"
    if ($pieces.Count -ne 9) { throw "Count=$($pieces.Count)" }
    Pass "PP6 Count restored" "9"
}

# --- Straight fit piece count ---
Write-Host ""
Write-Host "--- PP7. Straight fit (8 pieces) ---" -ForegroundColor Cyan

Try-Test "PP7 Straight draft" {
    Invoke-WebRequest -Uri "$BaseUrl/Pieces/DraftPieces?patternId=$patternId&style=straight" -Method POST -WebSession $session -UseBasicParsing -MaximumRedirection 5 | Out-Null
    $pieces = Get-PieceData $session $patternId "straight"
    if ($pieces.Count -ne 8) { throw "Expected 8 for straight, got $($pieces.Count)" }
    $names = Get-PieceNames $pieces
    if ($names -contains "Coin Pocket") { throw "Straight should not have Coin Pocket" }
    Pass "PP7 Straight" "8 pieces, no Coin Pocket"
}

# --- Dashboard piece count ---
Write-Host ""
Write-Host "--- PP8. Dashboard sync ---" -ForegroundColor Cyan

Try-Test "PP8 Pattern piece count on dashboard" {
    $rows = Invoke-RestMethod -Uri "$BaseUrl/Home/Patterns" -WebSession $session
    $p = @($rows) | Where-Object { $_.id -eq $patternId } | Select-Object -First 1
    if (-not $p) { throw "Pattern not in list" }
    if ([int]$p.pieceCount -lt 8) { throw "pieceCount=$($p.pieceCount)" }
    Pass "PP8 Dashboard pieceCount" "$($p.pieceCount)"
}

# --- Cleanup ---
Write-Host ""
Write-Host "--- PP9. Cleanup ---" -ForegroundColor Cyan

Try-Test "PP9 Delete test pattern" {
    Delete-Pattern "$BaseUrl/Home/Delete/$patternId" $session
    Pass "PP9 Delete pattern" "id=$patternId"
}

# --- Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PATTERN PIECES MODULE SUMMARY" -ForegroundColor Cyan
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

Write-Host "PATTERN PIECES MODULE PASSED." -ForegroundColor Green
Write-Host "Browser-only: filter groups, grid/list toggle, Add Piece modal preview." -ForegroundColor DarkGray
Write-Host ""
exit 0
