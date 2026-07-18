# PatternPro Style Sheet module — full HTTP/API test
# Usage: powershell -ExecutionPolicy Bypass -File tools/qa-style-sheet-test.ps1

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

function Get-PatternList($data) {
    if ($null -eq $data) { return @() }
    if ($data -is [System.Array]) { return @($data) }
    return @($data)
}

function Get-PatternById($Session, [int]$Id) {
    $rows = Get-PatternList (Invoke-RestMethod -Uri "$BaseUrl/StyleSheet/Rows" -WebSession $Session)
    return $rows | Where-Object { $_.id -eq $Id } | Select-Object -First 1
}

function Delete-Pattern([string]$Uri, $Session) {
    Invoke-WebRequest -Uri $Uri -Method DELETE -WebSession $Session -UseBasicParsing | Out-Null
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PatternPro Style Sheet Module Test" -ForegroundColor Cyan
Write-Host "  Target: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$session = $null
Try-Test "SS0 Login" {
    $script:session = Login-Admin
    Pass "SS0 Login" $UserName
}

if (-not $session) {
    Write-Host "STOPPED: Login failed." -ForegroundColor Red
    exit 1
}

# --- Page shell ---
Write-Host "--- SS1. Style Sheet page ---" -ForegroundColor Cyan

$pageHtml = ""
Try-Test "SS1 Style Sheet page loads" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/StyleSheet" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    $script:pageHtml = $r.Content
    Pass "SS1 Style Sheet page loads"
}

$uiMarkers = @(
    @{ Name = "lifecycle legend";     Pattern = "style-sheet-legend" },
    @{ Name = "search input";         Pattern = "ss-search" },
    @{ Name = "lifecycle tabs";       Pattern = "ss-lifecycle-tabs" },
    @{ Name = "style table";          Pattern = "style-sheet-table" },
    @{ Name = "table body";           Pattern = "ss-tbody" },
    @{ Name = "row count";            Pattern = "ss-count" },
    @{ Name = "new style button";     Pattern = "ss-btn-add" },
    @{ Name = "style-sheet.js";       Pattern = "style-sheet.js" },
    @{ Name = "dashboard link";       Pattern = "Pattern dashboard" }
)
foreach ($m in $uiMarkers) {
    Try-Test "SS1 UI: $($m.Name)" {
        if ($pageHtml -notmatch [regex]::Escape($m.Pattern)) { throw "Missing: $($m.Pattern)" }
        Pass "SS1 UI: $($m.Name)"
    }
}

# --- Rows API ---
Write-Host ""
Write-Host "--- SS2. StyleSheet/Rows API ---" -ForegroundColor Cyan

Try-Test "SS2 Rows list matches page total" {
    $rows = Get-PatternList (Invoke-RestMethod -Uri "$BaseUrl/StyleSheet/Rows" -WebSession $session)
    $m = [regex]::Match($pageHtml, 'id="ss-count"[^>]*data-total="(\d+)"')
    if (-not $m.Success) { throw "ss-count data-total not found" }
    $onPage = [int]$m.Groups[1].Value
    if ($rows.Count -ne $onPage) { throw "API=$($rows.Count) page=$onPage" }
    Pass "SS2 Rows list" "$($rows.Count) styles"
}

Try-Test "SS2 Rows search by season" {
    $hits = Get-PatternList (Invoke-RestMethod -Uri "$BaseUrl/StyleSheet/Rows?q=SS" -WebSession $session)
    if ($hits.Count -lt 1) { throw "No rows match season search 'SS'" }
    Pass "SS2 Rows search" "$($hits.Count) hit(s) for SS"
}

Try-Test "SS2 Rows sort by code asc" {
    $sorted = Get-PatternList (Invoke-RestMethod -Uri "$BaseUrl/StyleSheet/Rows?sort=code&asc=true" -WebSession $session)
    if ($sorted.Count -lt 2) { Pass "SS2 Rows sort asc" "only $($sorted.Count) row(s)"; return }
    for ($i = 1; $i -lt $sorted.Count; $i++) {
        if ($sorted[$i].code -lt $sorted[$i - 1].code) { throw "Not ascending at index $i" }
    }
    Pass "SS2 Rows sort asc" "$($sorted.Count) rows"
}

Try-Test "SS2 Rows sort by lifecycle desc" {
    $sorted = Get-PatternList (Invoke-RestMethod -Uri "$BaseUrl/StyleSheet/Rows?sort=lifecycle&asc=false" -WebSession $session)
    if ($sorted.Count -lt 1) { throw "Empty list" }
    Pass "SS2 Rows sort lifecycle" "$($sorted.Count) rows"
}

# --- Create with PLM fields ---
Write-Host ""
Write-Host "--- SS3. Create style row ---" -ForegroundColor Cyan

$testName = "StyleSheet QA $(Get-Date -Format 'yyyyMMdd-HHmmss')"
Try-Test "SS3 Create style with PLM fields" {
    $body = @{
        name            = $testName
        styleKey        = "slim"
        baseSize        = "M"
        categoryKey     = "denim"
        designer        = "SS QA Designer"
        season          = "SS26"
        owner           = "SS QA Owner"
        lifecycleStatus = "Idea"
    }
    $created = Post-Json "$BaseUrl/Home/Create" $session $body
    $script:TestPatternId = [int]$created.id
    if ($script:TestPatternId -le 0) { throw "No id" }
    if ($created.season -ne "SS26") { throw "Season=$($created.season)" }
    if ($created.owner -ne "SS QA Owner") { throw "Owner=$($created.owner)" }
    if ($created.lifecycleStatus -ne "Idea") { throw "Lifecycle=$($created.lifecycleStatus)" }
    Pass "SS3 Create style" "id=$($script:TestPatternId) code=$($created.code)"
}

if ($script:TestPatternId -le 0) {
    Write-Host "STOPPED: Create failed." -ForegroundColor Red
    exit 1
}
$patternId = $script:TestPatternId

Try-Test "SS3 New row appears in StyleSheet/Rows" {
    $rows = Get-PatternList (Invoke-RestMethod -Uri "$BaseUrl/StyleSheet/Rows" -WebSession $session)
    $found = $rows | Where-Object { $_.id -eq $patternId }
    if (-not $found) { throw "Pattern $patternId not in StyleSheet/Rows" }
    Pass "SS3 Row in register" $found.code
}

# --- Update style sheet fields ---
Write-Host ""
Write-Host "--- SS4. Update PLM fields ---" -ForegroundColor Cyan

Try-Test "SS4 UpdateStyleSheet (season, owner, designer)" {
    $updated = Post-Json "$BaseUrl/Home/UpdateStyleSheet" $session @{
        id = $patternId; season = "FW26"; owner = "Merch Lead"; designer = "Lead Designer"
    }
    if ($updated.season -ne "FW26") { throw "Season=$($updated.season)" }
    if ($updated.owner -ne "Merch Lead") { throw "Owner=$($updated.owner)" }
    if ($updated.designer -ne "Lead Designer") { throw "Designer=$($updated.designer)" }
    Pass "SS4 UpdateStyleSheet" "FW26 / Merch Lead / Lead Designer"
}

Try-Test "SS4 Fields persist in StyleSheet/Rows" {
    $row = Get-PatternById $session $patternId
    if (-not $row) { throw "Row not found after update" }
    if ($row.season -ne "FW26") { throw "Persisted season=$($row.season)" }
    if ($row.owner -ne "Merch Lead") { throw "Persisted owner=$($row.owner)" }
    if ($row.designer -ne "Lead Designer") { throw "Persisted designer=$($row.designer)" }
    Pass "SS4 Persisted in register"
}

# --- Lifecycle transitions ---
Write-Host ""
Write-Host "--- SS5. Lifecycle ---" -ForegroundColor Cyan

Try-Test "SS5 SetLifecycle Idea → Sampling" {
    $updated = Post-Json "$BaseUrl/Home/SetLifecycle" $session @{ id = $patternId; lifecycleStatus = "Sampling" }
    if ($updated.lifecycleStatus -ne "Sampling") { throw "Got $($updated.lifecycleStatus)" }
    Pass "SS5 Sampling"
}

Try-Test "SS5 Bulk blocked without certification" {
    Post-Json-ExpectFail "$BaseUrl/Home/SetLifecycle" $session @{ id = $patternId; lifecycleStatus = "Bulk" }
    $p = Get-PatternById $session $patternId
    if ($p.lifecycleStatus -eq "Bulk") { throw "Bulk was set without certification" }
    if ($p.lifecycleStatus -ne "Sampling") { throw "Expected Sampling, got $($p.lifecycleStatus)" }
    Pass "SS5 Bulk blocked" "still $($p.lifecycleStatus)"
}

Try-Test "SS5 SetLifecycle → Cancelled" {
    $updated = Post-Json "$BaseUrl/Home/SetLifecycle" $session @{ id = $patternId; lifecycleStatus = "Cancelled" }
    if ($updated.lifecycleStatus -ne "Cancelled") { throw "Got $($updated.lifecycleStatus)" }
    Pass "SS5 Cancelled"
}

Try-Test "SS5 Invalid lifecycle rejected" {
    Post-Json-ExpectFail "$BaseUrl/Home/SetLifecycle" $session @{ id = $patternId; lifecycleStatus = "InvalidStage" }
    Pass "SS5 Invalid lifecycle rejected"
}

# --- Bulk lifecycle when certified ---
Write-Host ""
Write-Host "--- SS6. Bulk lifecycle (certified) ---" -ForegroundColor Cyan

Try-Test "SS6 Prepare pattern for Bulk gate" {
    Post-Json "$BaseUrl/Home/SetLifecycle" $session @{ id = $patternId; lifecycleStatus = "Sampling" } | Out-Null
    Post-Json "$BaseUrl/Home/SetStatus" $session @{ id = $patternId; status = "Graded" } | Out-Null
    Invoke-WebRequest -Uri "$BaseUrl/Pieces/DraftPieces?patternId=$patternId&style=slim" -Method POST -WebSession $session -UseBasicParsing | Out-Null
    $resp = Post-Json "$BaseUrl/Export/ApproveForCutting" $session @{ patternId = $patternId; style = "slim"; actor = "SS QA" }
    if (-not $resp.approvedForCutting) { throw "Approve failed" }
    $resp2 = Post-Json "$BaseUrl/Export/RecordCutterTest" $session @{
        patternId = $patternId; passed = $true; actor = "SS QA Factory"; notes = "Style sheet test"
    }
    if (-not $resp2.cutterTestPassed) { throw "Cutter test failed" }
    Pass "SS6 Certified" "approve + cutter pass"
}

Try-Test "SS6 SetLifecycle → Bulk when certified" {
    $updated = Post-Json "$BaseUrl/Home/SetLifecycle" $session @{ id = $patternId; lifecycleStatus = "Bulk" }
    if ($updated.lifecycleStatus -ne "Bulk") { throw "Got $($updated.lifecycleStatus)" }
    Pass "SS6 Bulk lifecycle" $updated.code
}

# --- Navigation ---
Write-Host ""
Write-Host "--- SS7. Links ---" -ForegroundColor Cyan

Try-Test "SS7 Pattern link (Pieces page)" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Pieces?patternId=$patternId&style=slim" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    Pass "SS7 Pieces page" "patternId=$patternId"
}

Try-Test "SS7 Dashboard link from Style Sheet" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/" -WebSession $session -UseBasicParsing
    if ($r.Content -notmatch "Dashboard|Pattern") { throw "Dashboard did not load" }
    Pass "SS7 Dashboard reachable"
}

# --- Cleanup ---
Write-Host ""
Write-Host "--- SS8. Cleanup ---" -ForegroundColor Cyan

Try-Test "SS8 Delete test style" {
    Delete-Pattern "$BaseUrl/Home/Delete/$patternId" $session
    $rows = Get-PatternList (Invoke-RestMethod -Uri "$BaseUrl/StyleSheet/Rows" -WebSession $session)
    $gone = $rows | Where-Object { $_.id -eq $patternId }
    if ($gone) { throw "Test style still in register" }
    Pass "SS8 Delete test style" "id=$patternId removed"
}

# --- Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  STYLE SHEET MODULE SUMMARY" -ForegroundColor Cyan
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

Write-Host "STYLE SHEET MODULE PASSED." -ForegroundColor Green
Write-Host "Browser-only: lifecycle tabs filter, inline edit toasts, + New style modal." -ForegroundColor DarkGray
Write-Host ""
exit 0
