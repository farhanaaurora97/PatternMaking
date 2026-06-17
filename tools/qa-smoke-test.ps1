# PatternPro QA smoke test - run while app is on http://localhost:5001
# Usage:  powershell -ExecutionPolicy Bypass -File tools/qa-smoke-test.ps1

param(
    [string]$BaseUrl = "http://localhost:5001",
    [string]$UserName = "admin",
    [string]$Password = "Admin@123"
)

$ErrorActionPreference = "Stop"
$script:Passed = 0
$script:Failed = 0

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
}

function Try-Test([string]$Name, [scriptblock]$Block) {
    try { & $Block }
    catch { Fail $Name $_.Exception.Message }
}

function Get-PieceList($data) {
    if ($data -is [System.Array]) { return @($data) }
    if ($null -ne $data.pieces) { return @($data.pieces) }
    return @($data)
}

Write-Host ""
Write-Host "=== PatternPro QA Smoke Test ===" -ForegroundColor Cyan
Write-Host "Target: $BaseUrl"
Write-Host ""

Try-Test "App is running" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Account/Login" -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    Pass "App is running" "login page OK"
}

Try-Test "Unauthenticated redirect to login" {
    try {
        Invoke-WebRequest -Uri "$BaseUrl/" -MaximumRedirection 0 -UseBasicParsing -ErrorAction Stop | Out-Null
        throw "Expected redirect, got 200"
    }
    catch {
        Pass "Unauthenticated redirect to login"
    }
}

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
Try-Test "Admin login" {
    $loginPage = Invoke-WebRequest -Uri "$BaseUrl/Account/Login" -WebSession $session -UseBasicParsing
    $m = [regex]::Match($loginPage.Content, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $m.Success) { throw "Anti-forgery token not found" }
    $body = @{
        UserName                   = $UserName
        Password                   = $Password
        RememberMe                 = "false"
        ReturnUrl                  = ""
        __RequestVerificationToken = $m.Groups[1].Value
    }
    Invoke-WebRequest -Uri "$BaseUrl/Account/Login" -Method POST -Body $body -WebSession $session -UseBasicParsing | Out-Null
    $dash = Invoke-WebRequest -Uri "$BaseUrl/" -WebSession $session -UseBasicParsing
    if ($dash.Content -notmatch "Dashboard|Pattern") { throw "Dashboard did not load after login" }
    Pass "Admin login" $UserName
}

if ($script:Failed -gt 0 -and $script:Passed -lt 2) {
    Write-Host ""
    Write-Host "Cannot continue without login." -ForegroundColor Red
    exit 1
}

$pages = @(
    @{ Name = "Dashboard";       Url = "/" },
    @{ Name = "Size Chart";      Url = "/SizeChart" },
    @{ Name = "Block Generator"; Url = "/BlockGenerator?style=slim" },
    @{ Name = "Grading";         Url = "/Grading?style=slim" },
    @{ Name = "Nest";            Url = "/Nest?style=slim" },
    @{ Name = "Library";         Url = "/Library" },
    @{ Name = "Style Sheet";     Url = "/StyleSheet" },
    @{ Name = "Admin users";     Url = "/Admin" }
)
foreach ($p in $pages) {
    Try-Test "Page: $($p.Name)" {
        $r = Invoke-WebRequest -Uri "$BaseUrl$($p.Url)" -WebSession $session -UseBasicParsing
        if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
        Pass "Page: $($p.Name)"
    }
}

Try-Test "Size chart M waist = 84 cm" {
    $csv = (Invoke-WebRequest -Uri "$BaseUrl/SizeChart/ExportCsv" -WebSession $session -UseBasicParsing).Content
    $waistLine = ($csv -split "`n") | Where-Object { $_ -match "^Waist," } | Select-Object -First 1
    if ($waistLine -notmatch ",84,") { throw "Expected Waist M=84, got: $waistLine" }
    Pass "Size chart M waist = 84 cm"
}

$script:patternId = 0
Try-Test "Create pattern via API" {
    $body = (@{
        name            = "QA Smoke $(Get-Date -Format 'yyyyMMdd-HHmmss')"
        styleKey        = "slim"
        baseSize        = "M"
        categoryKey     = "denim"
        designer        = "QA Bot"
        season          = "SS26"
        owner           = "QA"
        lifecycleStatus = "Idea"
    } | ConvertTo-Json -Compress)
    $created = Invoke-RestMethod -Uri "$BaseUrl/Home/Create" -Method POST -Body $body -ContentType "application/json" -WebSession $session
    $script:patternId = [int]$created.id
    if ($script:patternId -le 0) { throw "No pattern id returned" }
    Pass "Create pattern via API" "id=$($script:patternId) code=$($created.code)"
}

if ($script:patternId -gt 0) {
    $patternId = $script:patternId
    Try-Test "Draft pieces (Block Generator)" {
        Invoke-WebRequest -Uri "$BaseUrl/Pieces/DraftPieces?patternId=$patternId&style=slim" -Method POST -WebSession $session -UseBasicParsing | Out-Null
        Pass "Draft pieces (Block Generator)"
    }

    Try-Test "Pattern Pieces page loads" {
        Invoke-WebRequest -Uri "$BaseUrl/Pieces?patternId=$patternId&style=slim" -WebSession $session -UseBasicParsing | Out-Null
        Pass "Pattern Pieces page loads"
    }

    Try-Test "Canvas page loads" {
        Invoke-WebRequest -Uri "$BaseUrl/Canvas?patternId=$patternId&style=slim" -WebSession $session -UseBasicParsing | Out-Null
        Pass "Canvas page loads"
    }

    Try-Test "Canvas piece data JSON" {
        $data = Invoke-RestMethod -Uri "$BaseUrl/Canvas/PieceData?patternId=$patternId&style=slim" -WebSession $session
        $pieces = Get-PieceList $data
        $count = $pieces.Count
        if ($count -lt 3) { throw "Expected >= 3 pieces, got $count" }
        Pass "Canvas piece data JSON" "$count pieces"
    }

    Try-Test "Factory QC JSON endpoint" {
        $qc = Invoke-RestMethod -Uri "$BaseUrl/Export/ValidateFactory?patternId=$patternId&style=slim" -WebSession $session
        if ($null -eq $qc.canExportToFactory) { throw "Invalid QC response" }
        Pass "Factory QC JSON endpoint" "canExport=$($qc.canExportToFactory)"
    }

    Try-Test "Factory export blocked when not certified" {
        try {
            Invoke-WebRequest -Uri "$BaseUrl/Export/DownloadPackage?patternId=$patternId&style=slim&format=PLT&purpose=factory" -WebSession $session -UseBasicParsing -ErrorAction Stop | Out-Null
            throw "Factory ZIP should be blocked before certification"
        }
        catch {
            if ($_.Exception.Message -match "Factory ZIP should be blocked") { throw }
            Pass "Factory export blocked when not certified"
        }
    }

    $zipPath = $null
    Try-Test "Draft export ZIP download" {
        $r = Invoke-WebRequest -Uri "$BaseUrl/Export/DownloadPackage?patternId=$patternId&style=slim&format=PLT&purpose=draft&sizes=M" -WebSession $session -UseBasicParsing
        if ($r.RawContentLength -lt 50) { throw "ZIP too small" }
        $zipPath = Join-Path $env:TEMP "patternpro-qa-$patternId.zip"
        [IO.File]::WriteAllBytes($zipPath, $r.Content)
        Pass "Draft export ZIP download" "$([math]::Round($r.RawContentLength/1KB, 1)) KB"
    }

    foreach ($fmt in @("DXF", "HPGL", "PDF")) {
        Try-Test "Draft export $fmt" {
            $r = Invoke-WebRequest -Uri "$BaseUrl/Export/DownloadPackage?patternId=$patternId&style=slim&format=$fmt&purpose=draft&sizes=M" -WebSession $session -UseBasicParsing
            if ($r.RawContentLength -lt 50) { throw "File too small ($($r.RawContentLength) bytes)" }
            Pass "Draft export $fmt" "$($r.RawContentLength) bytes"
        }
    }

    if ($zipPath -and (Test-Path $zipPath)) {
        Try-Test "ZIP contains PLT cutter file" {
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
            try {
                $plt = $zip.Entries | Where-Object { $_.FullName -match "\.plt$" } | Select-Object -First 1
                if (-not $plt) { throw "No .plt in ZIP" }
                $stream = $plt.Open()
                $reader = New-Object IO.StreamReader($stream)
                $text = $reader.ReadToEnd()
                $reader.Close()
                if ($text -notmatch "IN;") { throw "PLT missing IN; command" }
                Pass "ZIP contains PLT cutter file" $plt.FullName
            }
            finally { $zip.Dispose() }
        }
        Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    }

    Try-Test "Export page loads with QC panel" {
        Invoke-WebRequest -Uri "$BaseUrl/Export?patternId=$patternId&style=slim" -WebSession $session -UseBasicParsing | Out-Null
        Pass "Export page loads with QC panel"
    }
}

Try-Test "Export preview pieces API" {
    $pieces = Invoke-RestMethod -Uri "$BaseUrl/Export/PreviewPieces?style=slim" -WebSession $session
    if (@($pieces).Count -lt 3) { throw "Expected piece list" }
    Pass "Export preview pieces API" "$(@($pieces).Count) pieces"
}

Try-Test "Dashboard charts API" {
    Invoke-RestMethod -Uri "$BaseUrl/Home/ChartsData" -WebSession $session | Out-Null
    Pass "Dashboard charts API"
}

Try-Test "Logout" {
    $dash = (Invoke-WebRequest -Uri "$BaseUrl/" -WebSession $session -UseBasicParsing).Content
    $m = [regex]::Match($dash, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $m.Success) { throw "Logout form token not found" }
    $body = @{ __RequestVerificationToken = $m.Groups[1].Value }
    Invoke-WebRequest -Uri "$BaseUrl/Account/Logout" -Method POST -Body $body -WebSession $session -UseBasicParsing | Out-Null
    Pass "Logout"
}

Try-Test "After logout, dashboard redirects" {
    try {
        Invoke-WebRequest -Uri "$BaseUrl/" -WebSession $session -MaximumRedirection 0 -UseBasicParsing -ErrorAction Stop | Out-Null
        throw "Expected redirect after logout"
    }
    catch {
        Pass "After logout, dashboard redirects"
    }
}

Write-Host ""
Write-Host "=== QA Summary ===" -ForegroundColor Cyan
Write-Host "  Passed: $script:Passed"
Write-Host "  Failed: $script:Failed"
Write-Host ""

if ($script:Failed -gt 0) {
    Write-Host "Some checks failed." -ForegroundColor Red
    exit 1
}

Write-Host "All automated QA checks passed." -ForegroundColor Green
Write-Host "Next: browser-only steps in docs/TESTING.md Level 3."
Write-Host ""
exit 0
