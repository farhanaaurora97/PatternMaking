# PatternPro Dashboard module — full HTTP/API test
# Usage: powershell -ExecutionPolicy Bypass -File tools/qa-dashboard-test.ps1

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
$script:DuplicatePatternId = 0

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

function Get-PatternList($data) {
    if ($null -eq $data) { return @() }
    if ($data -is [System.Array]) { return @($data) }
    return @($data)
}

function Delete-Pattern([string]$Uri, $Session) {
    Invoke-WebRequest -Uri $Uri -Method DELETE -WebSession $Session -UseBasicParsing | Out-Null
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PatternPro Dashboard Module Test" -ForegroundColor Cyan
Write-Host "  Target: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$session = $null
Try-Test "D0 Login" {
    $script:session = Login-Admin
    Pass "D0 Login" $UserName
}

if (-not $session) {
    Write-Host "STOPPED: Login failed." -ForegroundColor Red
    exit 1
}

# --- Page & UI shell ---
Write-Host "--- D1. Dashboard page ---" -ForegroundColor Cyan

Try-Test "D1 Dashboard page loads" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    Pass "D1 Dashboard page loads"
}

$requiredMarkers = @(
    @{ Name = "stat cards";        Pattern = "stat-card" },
    @{ Name = "Factory ready stat"; Pattern = "stat-factory-ready" },
    @{ Name = "Analytics charts";  Pattern = "chart-status" },
    @{ Name = "Patterns table";    Pattern = "patterns-tbody" },
    @{ Name = "Search input";      Pattern = "tbl-search-input" },
    @{ Name = "Category tabs";     Pattern = "cat-tabs" },
    @{ Name = "Style progress";    Pattern = "prog-row" },
    @{ Name = "Recent activity";   Pattern = "act-row" },
    @{ Name = "Due this week";     Pattern = "due-week-strip" },
    @{ Name = "Chart.js bundle";   Pattern = "dashboard-charts.js" }
)

$dashHtml = (Invoke-WebRequest -Uri "$BaseUrl/" -WebSession $session -UseBasicParsing).Content
foreach ($m in $requiredMarkers) {
    Try-Test "D1 UI: $($m.Name)" {
        if ($dashHtml -notmatch [regex]::Escape($m.Pattern)) { throw "Missing: $($m.Pattern)" }
        Pass "D1 UI: $($m.Name)"
    }
}

# --- Charts API ---
Write-Host ""
Write-Host "--- D2. Charts API ---" -ForegroundColor Cyan

Try-Test "D2 ChartsData JSON" {
    $charts = Invoke-RestMethod -Uri "$BaseUrl/Home/ChartsData" -WebSession $session
    if (-not $charts.status) { throw "Missing status slices" }
    if (-not $charts.stylesByFit) { throw "Missing stylesByFit" }
    if (-not $charts.pantTypes) { throw "Missing pantTypes" }
    $statusKeys = @($charts.status | ForEach-Object { $_.key })
    foreach ($k in @("Pending", "Draft", "InProgress", "Graded", "Done")) {
        if ($statusKeys -notcontains $k) { throw "Missing status key: $k" }
    }
    if ($charts.stylesByFit.labels.Count -lt 5) { throw "Expected 5 fit labels" }
    if ($charts.stylesByFit.datasets.Count -lt 5) { throw "Expected 5 status datasets" }
    Pass "D2 ChartsData JSON" "$($charts.status.Count) status slices, $($charts.pantTypes.Count) pant types"
}

Try-Test "D2 Chart data embedded in page" {
    if ($dashHtml -notmatch 'id="dashboard-chart-data"') { throw "Embedded chart JSON script missing" }
    Pass "D2 Chart data embedded in page"
}

# --- Create pattern ---
Write-Host ""
Write-Host "--- D3. Create pattern ---" -ForegroundColor Cyan

$testName = "Dashboard QA $(Get-Date -Format 'yyyyMMdd-HHmmss')"
Try-Test "D3 Create pattern (Home/Create)" {
    $body = @{
        name            = $testName
        styleKey        = "slim"
        baseSize        = "M"
        categoryKey     = "denim"
        designer        = "QA Dashboard"
        season          = "SS26"
        owner           = "QA Team"
        lifecycleStatus = "Idea"
    }
    $created = Post-Json "$BaseUrl/Home/Create" $session $body
    $script:TestPatternId = [int]$created.id
    if ($script:TestPatternId -le 0) { throw "No pattern id returned" }
    if (-not $created.code) { throw "No pattern code" }
    if ($created.name -ne $testName) { throw "Name mismatch: $($created.name)" }
    if ($created.styleKey -ne "slim") { throw "styleKey mismatch" }
    Pass "D3 Create pattern" "id=$($script:TestPatternId) code=$($created.code)"
}

if ($script:TestPatternId -le 0) {
    Write-Host "STOPPED: Pattern create failed." -ForegroundColor Red
    exit 1
}
$patternId = $script:TestPatternId

# --- Patterns list / search / sort ---
Write-Host ""
Write-Host "--- D4. Patterns table API ---" -ForegroundColor Cyan

Try-Test "D4 Patterns list (all)" {
    $all = Get-PatternList (Invoke-RestMethod -Uri "$BaseUrl/Home/Patterns" -WebSession $session)
    if ($all.Count -lt 1) { throw "Empty pattern list" }
    $found = $all | Where-Object { $_.id -eq $patternId }
    if (-not $found) { throw "Created pattern $patternId not in list" }
    Pass "D4 Patterns list" "$($all.Count) patterns"
}

Try-Test "D4 Patterns search by name" {
    $q = [uri]::EscapeDataString($testName.Substring(0, [Math]::Min(12, $testName.Length)))
    $hits = Get-PatternList (Invoke-RestMethod -Uri "$BaseUrl/Home/Patterns?q=$q" -WebSession $session)
    if ($hits.Count -lt 1) { throw "Search returned no hits for '$q'" }
    $found = $hits | Where-Object { $_.id -eq $patternId }
    if (-not $found) { throw "Created pattern not in search results" }
    Pass "D4 Patterns search" "$($hits.Count) hit(s)"
}

Try-Test "D4 Patterns sort by code asc" {
    $sorted = Get-PatternList (Invoke-RestMethod -Uri "$BaseUrl/Home/Patterns?sort=code&asc=true" -WebSession $session)
    if ($sorted.Count -lt 2) { Pass "D4 Patterns sort asc" "only $($sorted.Count) row(s)"; return }
    for ($i = 1; $i -lt $sorted.Count; $i++) {
        if ($sorted[$i].code -lt $sorted[$i - 1].code) { throw "Not ascending at index $i" }
    }
    Pass "D4 Patterns sort asc" "$($sorted.Count) rows"
}

Try-Test "D4 Patterns sort by code desc" {
    $sorted = Get-PatternList (Invoke-RestMethod -Uri "$BaseUrl/Home/Patterns?sort=code&asc=false" -WebSession $session)
    if ($sorted.Count -lt 2) { Pass "D4 Patterns sort desc" "only $($sorted.Count) row(s)"; return }
    for ($i = 1; $i -lt $sorted.Count; $i++) {
        if ($sorted[$i].code -gt $sorted[$i - 1].code) { throw "Not descending at index $i" }
    }
    Pass "D4 Patterns sort desc" "$($sorted.Count) rows"
}

# --- Status & lifecycle ---
Write-Host ""
Write-Host "--- D5. Status & lifecycle ---" -ForegroundColor Cyan

Try-Test "D5 SetStatus → InProgress" {
    $updated = Post-Json "$BaseUrl/Home/SetStatus" $session @{ id = $patternId; status = "InProgress" }
    if ($updated.status -ne "InProgress") { throw "Status is $($updated.status)" }
    Pass "D5 SetStatus" "InProgress"
}

Try-Test "D5 CycleStatus advances" {
    $before = Get-PatternList (Invoke-RestMethod -Uri "$BaseUrl/Home/Patterns" -WebSession $session) | Where-Object { $_.id -eq $patternId }
    $updated = Invoke-RestMethod -Uri "$BaseUrl/Home/CycleStatus/$patternId" -Method POST -WebSession $session
    if ($updated.status -eq $before.status) { throw "Status did not change from $($before.status)" }
    Pass "D5 CycleStatus" "$($before.status) → $($updated.status)"
}

Try-Test "D5 SetLifecycle → Sampling" {
    $updated = Post-Json "$BaseUrl/Home/SetLifecycle" $session @{ id = $patternId; lifecycleStatus = "Sampling" }
    if ($updated.lifecycleStatus -ne "Sampling") { throw "Lifecycle is $($updated.lifecycleStatus)" }
    Pass "D5 SetLifecycle" "Sampling"
}

Try-Test "D5 UpdateStyleSheet" {
    $updated = Post-Json "$BaseUrl/Home/UpdateStyleSheet" $session @{
        id = $patternId; season = "FW26"; owner = "Merch Lead"; designer = "Lead Designer"
    }
    if ($updated.season -ne "FW26") { throw "Season is $($updated.season)" }
    if ($updated.owner -ne "Merch Lead") { throw "Owner is $($updated.owner)" }
    Pass "D5 UpdateStyleSheet" "FW26 / Merch Lead"
}

# --- Due date ---
Write-Host ""
Write-Host "--- D6. Due date ---" -ForegroundColor Cyan

Try-Test "D6 SetDueDate" {
    $due = (Get-Date).AddDays(3).ToString("yyyy-MM-dd")
    $updated = Post-Json "$BaseUrl/Home/SetDueDate" $session @{ id = $patternId; date = $due }
    if ($updated.dueDateIso -ne $due) { throw "Due date is $($updated.dueDateIso), expected $due" }
    Pass "D6 SetDueDate" $due
}

Try-Test "D6 Clear due date" {
    $updated = Post-Json "$BaseUrl/Home/SetDueDate" $session @{ id = $patternId; date = $null }
    if ($updated.dueDateIso) { throw "Due date not cleared: $($updated.dueDateIso)" }
    Pass "D6 Clear due date"
}

# --- Duplicate & delete ---
Write-Host ""
Write-Host "--- D7. Duplicate & delete ---" -ForegroundColor Cyan

Try-Test "D7 Duplicate pattern" {
    $copy = Invoke-RestMethod -Uri "$BaseUrl/Home/Duplicate/$patternId" -Method POST -WebSession $session
    $script:DuplicatePatternId = [int]$copy.id
    if ($script:DuplicatePatternId -le 0) { throw "No duplicate id" }
    if ($copy.id -eq $patternId) { throw "Duplicate has same id as source" }
    if ($copy.status -ne "Draft") { throw "Duplicate status should be Draft, got $($copy.status)" }
    Pass "D7 Duplicate pattern" "id=$($script:DuplicatePatternId) code=$($copy.code)"
}

Try-Test "D7 Delete duplicate" {
    if ($script:DuplicatePatternId -le 0) { throw "No duplicate to delete" }
    Delete-Pattern "$BaseUrl/Home/Delete/$($script:DuplicatePatternId)" $session
    $all = Get-PatternList (Invoke-RestMethod -Uri "$BaseUrl/Home/Patterns" -WebSession $session)
    $gone = $all | Where-Object { $_.id -eq $script:DuplicatePatternId }
    if ($gone) { throw "Duplicate still in list after delete" }
    Pass "D7 Delete duplicate"
}

# --- Factory ready stat ---
Write-Host ""
Write-Host "--- D8. Factory ready stat ---" -ForegroundColor Cyan

Try-Test "D8 Factory ready count matches API" {
    $all = Get-PatternList (Invoke-RestMethod -Uri "$BaseUrl/Home/Patterns" -WebSession $session)
    $certified = @($all | Where-Object { $_.isProductionCertified -eq $true }).Count
    $page = Invoke-WebRequest -Uri "$BaseUrl/" -WebSession $session -UseBasicParsing
    $m = [regex]::Match($page.Content, 'id="stat-factory-ready"[^>]*data-target="(\d+)"')
    if (-not $m.Success) { throw "stat-factory-ready data-target not found" }
    $onPage = [int]$m.Groups[1].Value
    if ($onPage -ne $certified) { throw "Page shows $onPage, API count is $certified ($($all.Count) patterns)" }
    Pass "D8 Factory ready stat" "$certified certified pattern(s)"
}

# --- Style Sheet link (Module 2 companion) ---
Write-Host ""
Write-Host "--- D9. Style Sheet page ---" -ForegroundColor Cyan

Try-Test "D9 Style Sheet page loads" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/StyleSheet" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    if ($r.Content -notmatch "Style Sheet|Lifecycle") { throw "Expected Style Sheet content" }
    Pass "D9 Style Sheet page loads"
}

# --- Cleanup test pattern ---
Write-Host ""
Write-Host "--- D10. Cleanup ---" -ForegroundColor Cyan

Try-Test "D10 Delete test pattern" {
    Delete-Pattern "$BaseUrl/Home/Delete/$patternId" $session
    $all = Get-PatternList (Invoke-RestMethod -Uri "$BaseUrl/Home/Patterns" -WebSession $session)
    $gone = $all | Where-Object { $_.id -eq $patternId }
    if ($gone) { throw "Test pattern still in list" }
    Pass "D10 Delete test pattern" "id=$patternId removed"
}

# --- Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  DASHBOARD MODULE SUMMARY" -ForegroundColor Cyan
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

Write-Host "DASHBOARD MODULE PASSED." -ForegroundColor Green
Write-Host "Browser-only: open / and verify charts render, category tabs filter rows, + Add modal." -ForegroundColor DarkGray
Write-Host ""
exit 0
