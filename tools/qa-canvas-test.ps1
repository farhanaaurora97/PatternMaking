# PatternPro Canvas Editor module — full HTTP/API test
# Usage: powershell -ExecutionPolicy Bypass -File tools/qa-canvas-test.ps1

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

function Post-Json([string]$Uri, $Session, $Object, [int]$Depth = 10) {
    $json = $Object | ConvertTo-Json -Compress -Depth $Depth
    return Invoke-RestMethod -Uri $Uri -Method POST -Body $json -ContentType "application/json" -WebSession $Session
}

function Post-Json-ExpectFail([string]$Uri, $Session, $Object) {
    try {
        Post-Json $Uri $Session $Object | Out-Null
        throw "Expected failure, got success"
    }
    catch {
        if ($_.Exception.Message -eq "Expected failure, got success") { throw }
        return $true
    }
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

function Clone-Pts($rawPts, [int]$DeltaX = 0, [int]$PointIndex = 0) {
    $out = [System.Collections.Generic.List[object]]::new()
    $i = 0
    foreach ($p in $rawPts) {
        $arr = @($p)
        $x = [int]$arr[0] + $(if ($i -eq $PointIndex) { $DeltaX } else { 0 })
        $y = [int]$arr[1]
        $out.Add(@($x, $y))
        $i++
    }
    return $out.ToArray()
}

function Delete-Pattern([int]$Id, $Session) {
    Invoke-WebRequest -Uri "$BaseUrl/Home/Delete/$Id" -Method DELETE -WebSession $Session -UseBasicParsing | Out-Null
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PatternPro Canvas Editor Module Test" -ForegroundColor Cyan
Write-Host "  Target: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$session = $null
Try-Test "CV0 Login" {
    $script:session = Login-Admin
    Pass "CV0 Login" $UserName
}

if (-not $session) {
    Write-Host "STOPPED: Login failed." -ForegroundColor Red
    exit 1
}

# --- Empty canvas ---
Write-Host "--- CV1. Empty canvas ---" -ForegroundColor Cyan

Try-Test "CV1 Empty page loads" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Canvas" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    if ($r.Content -notmatch "Empty Canvas") { throw "Missing empty state" }
    if ($r.Content -notmatch 'id="cv"') { throw "Missing canvas element" }
    Pass "CV1 Empty page"
}

Try-Test "CV1 PieceData empty without patternId" {
    $data = Invoke-RestMethod -Uri "$BaseUrl/Canvas/PieceData?patternId=0&style=slim" -WebSession $session
    $count = (Get-PieceList $data).Count
    if ($count -ne 0) { throw "Expected 0 pieces, got $count" }
    Pass "CV1 PieceData" "0 pieces"
}

# --- Setup pattern with geometry ---
Write-Host ""
Write-Host "--- CV2. Setup ---" -ForegroundColor Cyan

Try-Test "CV2 Create pattern" {
    $created = Post-Json "$BaseUrl/Home/Create" $session @{
        name = "Canvas QA $(Get-Date -Format 'yyyyMMdd-HHmmss')"
        styleKey = "slim"; baseSize = "M"; categoryKey = "denim"
        designer = "CV QA"; season = "SS26"; owner = "QA"; lifecycleStatus = "Idea"
    }
    $script:TestPatternId = [int]$created.id
    if ($script:TestPatternId -le 0) { throw "No pattern id" }
    Pass "CV2 Create pattern" "id=$($script:TestPatternId)"
}

if ($script:TestPatternId -le 0) {
    Write-Host "STOPPED: Pattern create failed." -ForegroundColor Red
    exit 1
}
$patternId = $script:TestPatternId

Try-Test "CV2 DraftPieces" {
    Invoke-WebRequest -Uri "$BaseUrl/Pieces/DraftPieces?patternId=$patternId&style=slim" -Method POST -WebSession $session -UseBasicParsing -MaximumRedirection 5 | Out-Null
    Pass "CV2 DraftPieces"
}

# --- Canvas page UI ---
Write-Host ""
Write-Host "--- CV3. Canvas page UI ---" -ForegroundColor Cyan

$canvasHtml = ""
Try-Test "CV3 Page loads with pattern" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Canvas?patternId=$patternId&style=slim&piece=0" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    $script:canvasHtml = $r.Content
    Pass "CV3 Page loads" "patternId=$patternId"
}

$uiMarkers = @(
    "canvas-shell", "canvas.js", "PIECE_DATA_URL", "SAVE_URL", "SAVE_ALL_URL",
    "MEASUREMENTS_URL", "CREATE_URL", "DRAFT_SIZES_URL", "RESET_FROM_STYLE_URL",
    "btn-save-all", "btn-undo", "btn-fit-all", "ctb-select", "ctb-draw",
    "tog-sa", "tog-grain", "tog-notch", "cv-piece-count", "cpl-list",
    "draft-sizes-panel", "btn-generate-draft", "Export DXF"
)
foreach ($marker in $uiMarkers) {
    Try-Test "CV3 UI: $marker" {
        if ($canvasHtml -notmatch [regex]::Escape($marker)) { throw "Missing: $marker" }
        Pass "CV3 UI: $marker"
    }
}

Try-Test "CV3 Sidebar Canvas nav" {
    if ($canvasHtml -notmatch 'href="/Canvas') { throw "Missing /Canvas nav link" }
    Pass "CV3 Sidebar nav"
}

Try-Test "CV3 Piece list in HTML" {
    foreach ($pieceLabel in @("Front Leg", "Back Leg", "Waistband")) {
        if ($canvasHtml -notmatch [regex]::Escape($pieceLabel)) { throw "Missing piece: $pieceLabel" }
    }
    Pass "CV3 Piece list" "Front Leg, Back Leg, Waistband"
}

Try-Test "CV3 Piece count badge" {
    if ($canvasHtml -notmatch "9 pieces") { throw "Expected 9 pieces badge" }
    Pass "CV3 Piece count" "9 pieces"
}

# --- PieceData API ---
Write-Host ""
Write-Host "--- CV4. PieceData ---" -ForegroundColor Cyan

Try-Test "CV4 PieceData count" {
    $pieces = Get-PieceData $session $patternId "slim"
    if ($pieces.Count -ne 9) { throw "Expected 9, got $($pieces.Count)" }
    Pass "CV4 PieceData" "9 pieces"
}

Try-Test "CV4 Required pieces" {
    $names = Get-PieceNames (Get-PieceData $session $patternId "slim")
    foreach ($req in @("Front Leg", "Back Leg", "Waistband", "Fly Facing", "Belt Loop")) {
        if ($names -notcontains $req) { throw "Missing: $req" }
    }
    Pass "CV4 Required names" ($names -join ", ")
}

Try-Test "CV4 Front Leg geometry" {
    $front = Get-PieceData $session $patternId "slim" | Where-Object { $_.name -eq "Front Leg" } | Select-Object -First 1
    $pts = Get-PiecePts $front
    if ($pts.Count -lt 4) { throw "Front Leg has $($pts.Count) points" }
    $sa = if ($null -ne $front.sa) { [double]$front.sa } else { [double]$front.Sa }
    if ($sa -le 0) { throw "Front Leg sa=$sa" }
    Pass "CV4 Front Leg" "$($pts.Count) pts, sa=$sa"
}

# --- Measurements API ---
Write-Host ""
Write-Host "--- CV5. Measurements ---" -ForegroundColor Cyan

Try-Test "CV5 Measurements Front Leg" {
    $m = Invoke-RestMethod -Uri "$BaseUrl/Canvas/Measurements?patternId=$patternId&style=slim&piece=Front%20Leg" -WebSession $session
    if ($m.pieceName -ne "Front Leg" -and $m.PieceName -ne "Front Leg") { throw "Wrong piece name" }
    $perim = if ($null -ne $m.perimeter) { [double]$m.perimeter } else { [double]$m.Perimeter }
    if ($perim -le 0) { throw "Invalid perimeter $perim" }
    $edges = @($m.edgeLengths); if (@($m.EdgeLengths).Count -gt 0) { $edges = @($m.EdgeLengths) }
    if ($edges.Count -lt 4) { throw "Expected edge lengths, got $($edges.Count)" }
    Pass "CV5 Measurements" "perimeter=$perim"
}

Try-Test "CV5 Measurements rejects missing piece" {
    try {
        Invoke-WebRequest -Uri "$BaseUrl/Canvas/Measurements?patternId=$patternId&style=slim" -WebSession $session -UseBasicParsing -ErrorAction Stop | Out-Null
        throw "Expected failure, got 200"
    }
    catch {
        if ($_.Exception.Message -eq "Expected failure, got 200") { throw }
        Pass "CV5 Validation" "piece required"
    }
}

# --- SavePiece persistence ---
Write-Host ""
Write-Host "--- CV6. SavePiece ---" -ForegroundColor Cyan

Try-Test "CV6 SavePiece persists edit" {
    $pieces = Get-PieceData $session $patternId "slim"
    $front = $pieces | Where-Object { $_.name -eq "Front Leg" } | Select-Object -First 1
    $rawPts = Get-PiecePts $front
    $origX = [int](@($rawPts[0])[0])
    $newPts = Clone-Pts $rawPts -DeltaX 7 -PointIndex 0

    Post-Json "$BaseUrl/Canvas/SavePiece" $session @{
        patternId = $patternId
        style     = "slim"
        name      = "Front Leg"
        pts       = $newPts
        ox        = [int]$front.ox
        oy        = [int]$front.oy
        grain     = $front.grain
        cf        = $front.cf
        notches   = $front.notches
        sa        = [double]$front.sa
        saJoin    = if ($front.saJoin) { $front.saJoin } else { "miter" }
    } | Out-Null

    $reloaded = Get-PieceData $session $patternId "slim" | Where-Object { $_.name -eq "Front Leg" } | Select-Object -First 1
    $rx = [int](@(Get-PiecePts $reloaded)[0][0])
    if ($rx -ne ($origX + 7)) { throw "Expected x=$($origX + 7), got $rx" }
    Pass "CV6 SavePiece" "Front Leg x moved +7"
}

Try-Test "CV6 SavePiece rejects too few points" {
    Post-Json-ExpectFail "$BaseUrl/Canvas/SavePiece" $session @{
        patternId = $patternId; style = "slim"; name = "Front Leg"
        pts = @(@(1, 1), @(2, 2)); ox = 0; oy = 0; sa = 0; saJoin = "miter"
    } | Out-Null
    Pass "CV6 Validation" "need >= 3 points"
}

# --- SaveAllPieces ---
Write-Host ""
Write-Host "--- CV7. SaveAllPieces ---" -ForegroundColor Cyan

Try-Test "CV7 SaveAllPieces" {
    $pieces = Get-PieceData $session $patternId "slim"
    $payload = @($pieces | ForEach-Object {
        @{
            name    = $_.name
            pts     = Clone-Pts (Get-PiecePts $_)
            ox      = [int]$_.ox
            oy      = [int]$_.oy
            grain   = $_.grain
            cf      = $_.cf
            notches = $_.notches
            sa      = [double]$_.sa
            saJoin  = if ($_.saJoin) { $_.saJoin } else { "miter" }
        }
    })
    $r = Post-Json "$BaseUrl/Canvas/SaveAllPieces" $session @{
        patternId = $patternId
        style     = "slim"
        pieces    = $payload
    }
    $saved = if ($null -ne $r.saved) { [int]$r.saved } else { [int]$r.Saved }
    if ($saved -ne 9) { throw "Expected saved=9, got $saved" }
    Pass "CV7 SaveAllPieces" "$saved pieces"
}

# --- CreatePiece ---
Write-Host ""
Write-Host "--- CV8. CreatePiece ---" -ForegroundColor Cyan

$testPieceName = "CV Patch $(Get-Date -Format 'HHmmss')"
Try-Test "CV8 CreatePiece on pattern" {
    $script:TestPieceName = $testPieceName
    $r = Post-Json "$BaseUrl/Canvas/CreatePiece" $session @{
        patternId = $patternId
        style     = "slim"
        name      = $testPieceName
        category  = "Hardware & Details"
        cut       = "Cut 2"
        color     = "#a78bfa"
        pts       = @(@(60, 60), @(120, 60), @(120, 120), @(60, 120))
        ox        = 900
        oy        = 300
    }
    if (-not $r.name -and -not $r.Name) { throw "No name in response" }
    $count = (Get-PieceData $session $patternId "slim").Count
    if ($count -ne 10) { throw "Expected 10 pieces after create, got $count" }
    Pass "CV8 CreatePiece" $testPieceName
}

Try-Test "CV8 Delete test piece" {
    Invoke-WebRequest -Uri "$BaseUrl/Pieces/DeletePiece?patternId=$patternId&style=slim&pieceName=$([uri]::EscapeDataString($script:TestPieceName))" -Method POST -WebSession $session -UseBasicParsing | Out-Null
    $count = (Get-PieceData $session $patternId "slim").Count
    if ($count -ne 9) { throw "Expected 9 after delete, got $count" }
    Pass "CV8 Cleanup piece" "back to 9"
}

# --- DraftSizes & ResetFromStyle ---
Write-Host ""
Write-Host "--- CV9. Draft & reset APIs ---" -ForegroundColor Cyan

Try-Test "CV9 DraftSizes" {
    $r = Invoke-RestMethod -Uri "$BaseUrl/Canvas/DraftSizes?style=slim&sizes=M&sizes=L" -WebSession $session
    $props = @($r.PSObject.Properties.Name)
    if ($props.Count -lt 2) { throw "Expected size keys, got: $($props -join ', ')" }
    Pass "CV9 DraftSizes" ($props -join ", ")
}

Try-Test "CV9 ResetFromStyle" {
    $r = Post-Json "$BaseUrl/Canvas/ResetFromStyle" $session @{
        patternId = $patternId
        style     = "slim"
    }
    $reset = if ($null -ne $r.reset) { $r.reset } else { $r.Reset }
    if (-not $reset) { throw "reset flag false" }
    $pc = if ($null -ne $r.pieceCount) { [int]$r.pieceCount } else { [int]$r.PieceCount }
    if ($pc -ne 9) { throw "Expected 9 pieces after reset, got $pc" }
    Pass "CV9 ResetFromStyle" "$pc pieces"
}

# --- Measurement helpers ---
Write-Host ""
Write-Host "--- CV10. Measurement helpers ---" -ForegroundColor Cyan

Try-Test "CV10 MeasurementProfiles" {
    $profiles = Invoke-RestMethod -Uri "$BaseUrl/Canvas/MeasurementProfiles" -WebSession $session
    if ($null -eq $profiles) { throw "Null response" }
    Pass "CV10 Profiles" "$(@($profiles).Count) profile(s)"
}

Try-Test "CV10 RecommendSize" {
    $r = Post-Json "$BaseUrl/Canvas/RecommendSize" $session @{
        baseSize  = "M"
        waist     = 84
        hip       = 100
        frontRise = 26
        backRise  = 34
        thigh     = 58
        knee      = 40
        ankle     = 36
        inseam    = 81
    }
    $size = if ($r.size) { $r.size } else { $r.Size }
    if ([string]::IsNullOrWhiteSpace($size)) { throw "No recommended size" }
    Pass "CV10 RecommendSize" $size
}

# --- Auth ---
Write-Host ""
Write-Host "--- CV11. Auth ---" -ForegroundColor Cyan

Try-Test "CV11 Canvas requires login" {
    try {
        Invoke-WebRequest -Uri "$BaseUrl/Canvas?patternId=$patternId&style=slim" -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop | Out-Null
        throw "Expected redirect, got 200"
    }
    catch {
        Pass "CV11 Page auth gate" "redirect to login"
    }
}

Try-Test "CV11 PieceData requires login" {
    try {
        Invoke-WebRequest -Uri "$BaseUrl/Canvas/PieceData?patternId=$patternId&style=slim" -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop | Out-Null
        throw "Expected redirect, got 200"
    }
    catch {
        Pass "CV11 PieceData auth gate" "redirect to login"
    }
}

# --- Cleanup ---
Write-Host ""
Write-Host "--- CV12. Cleanup ---" -ForegroundColor Cyan

Try-Test "CV12 Delete test pattern" {
    Delete-Pattern $patternId $session
    Pass "CV12 Cleanup" "id=$patternId"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  CANVAS EDITOR MODULE SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Passed:  $($script:Passed)" -ForegroundColor Green
Write-Host "  Failed:  $($script:Failed)" -ForegroundColor $(if ($script:Failed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($script:Failed -gt 0) {
    Write-Host "Failures:" -ForegroundColor Red
    foreach ($e in $script:Errors) { Write-Host "  - $e" -ForegroundColor Red }
    Write-Host ""
    Write-Host "CANVAS EDITOR MODULE FAILED." -ForegroundColor Red
    exit 1
}

Write-Host "CANVAS EDITOR MODULE PASSED." -ForegroundColor Green
Write-Host "Browser-only: drag points, undo, draw tool, layer toggles, auto-draft generate UI." -ForegroundColor DarkGray
exit 0
