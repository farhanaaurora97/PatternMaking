# PatternPro Export module — draft + factory certification test
# Usage: powershell -ExecutionPolicy Bypass -File tools/qa-export-test.ps1

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
    $json = $Object | ConvertTo-Json -Compress -Depth 10
    return Invoke-RestMethod -Uri $Uri -Method POST -Body $json -ContentType "application/json" -WebSession $Session
}

function Get-DownloadUrl([int]$PatternId, [string]$Style, [string]$Format, [string]$Purpose, [string]$Sizes = "M") {
    return "$BaseUrl/Export/DownloadPackage?patternId=$PatternId&style=$Style&format=$Format&purpose=$Purpose&sizes=$Sizes"
}

function Open-ZipBytes([byte[]]$Bytes) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $path = Join-Path $env:TEMP "patternpro-export-qa-$([Guid]::NewGuid().ToString('N')).zip"
    [IO.File]::WriteAllBytes($path, $Bytes)
    return @{
        Zip  = [IO.Compression.ZipFile]::OpenRead($path)
        Path = $path
    }
}

function Close-ZipHandle($handle) {
    if ($null -eq $handle) { return }
    if ($handle.Zip) { $handle.Zip.Dispose() }
    if ($handle.Path -and (Test-Path $handle.Path)) {
        Remove-Item $handle.Path -Force -ErrorAction SilentlyContinue
    }
}

function Read-ZipText($Zip, [string]$EntryPath) {
    $entry = $Zip.Entries | Where-Object {
        $_.FullName -eq $EntryPath -or $_.FullName -like "*$([IO.Path]::GetFileName($EntryPath))"
    } | Select-Object -First 1
    if (-not $entry) { return $null }
    $stream = $entry.Open()
    $reader = New-Object IO.StreamReader($stream)
    $text = $reader.ReadToEnd()
    $reader.Close()
    $stream.Close()
    return $text
}

function Assert-DxfStructure([string]$Dxf, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Dxf)) { throw "$Label : empty DXF" }
    if ($Dxf.Length -lt 1024) { throw "$Label : too small ($($Dxf.Length) bytes, need >1KB)" }
    foreach ($req in @("SECTION", "ENTITIES", "ENDSEC", "EOF", "`$INSUNITS", "AC1009")) {
        if ($Dxf -notmatch [regex]::Escape($req)) { throw "$Label : missing $req" }
    }
    if ($Dxf -notmatch "(?m)^LINE\r?\n8\r?\nCUT") { throw "$Label : missing CUT layer LINE entities" }
    $lineCount = ([regex]::Matches($Dxf, "(?m)^LINE\r?\n")).Count
    if ($lineCount -lt 4) { throw "$Label : expected multiple LINE entities, got $lineCount" }
}

function Get-ZipDxfEntries($Zip) {
    return @($Zip.Entries | Where-Object { $_.FullName -match '\.dxf$' })
}

function Delete-Pattern([int]$Id, $Session) {
    Invoke-WebRequest -Uri "$BaseUrl/Home/Delete/$Id" -Method DELETE -WebSession $Session -UseBasicParsing | Out-Null
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PatternPro Export Module Test" -ForegroundColor Cyan
Write-Host "  Target: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$session = $null
Try-Test "EX0 Login" {
    $script:session = Login-Admin
    Pass "EX0 Login" $UserName
}

if (-not $session) {
    Write-Host "STOPPED: Login failed." -ForegroundColor Red
    exit 1
}

# --- Setup ---
Write-Host "--- EX1. Setup ---" -ForegroundColor Cyan

Try-Test "EX1 Create pattern" {
    $created = Post-Json "$BaseUrl/Home/Create" $session @{
        name = "Export QA $(Get-Date -Format 'yyyyMMdd-HHmmss')"
        styleKey = "slim"; baseSize = "M"; categoryKey = "denim"
        designer = "EX QA"; season = "SS26"; owner = "QA"; lifecycleStatus = "Idea"
    }
    $script:TestPatternId = [int]$created.id
    if ($script:TestPatternId -le 0) { throw "No pattern id" }
    Pass "EX1 Create pattern" "id=$($script:TestPatternId)"
}

if ($script:TestPatternId -le 0) {
    Write-Host "STOPPED: Pattern create failed." -ForegroundColor Red
    exit 1
}
$patternId = $script:TestPatternId

Try-Test "EX1 DraftPieces" {
    Invoke-WebRequest -Uri "$BaseUrl/Pieces/DraftPieces?patternId=$patternId&style=slim" -Method POST -WebSession $session -UseBasicParsing -MaximumRedirection 5 | Out-Null
    Pass "EX1 DraftPieces"
}

# --- Export page ---
Write-Host ""
Write-Host "--- EX2. Export page UI ---" -ForegroundColor Cyan

$exportHtml = ""
Try-Test "EX2 Page loads" {
    $r = Invoke-WebRequest -Uri "$BaseUrl/Export?patternId=$patternId&style=slim" -WebSession $session -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "HTTP $($r.StatusCode)" }
    $script:exportHtml = $r.Content
    Pass "EX2 Page loads" "patternId=$patternId"
}

$uiMarkers = @(
    "Export Pattern", "production-qc-card", "Production QC", "export-grid",
    "btn-approve-cutting", "btn-cutter-pass", "btn-download-draft",
    "btn-download-export", "btn-download-clo", "export.js",
    "VALIDATE_FACTORY_URL", "DOWNLOAD_PACKAGE_URL", "DXF", "HPGL", "PLT", "PDF"
)
foreach ($marker in $uiMarkers) {
    Try-Test "EX2 UI: $marker" {
        if ($exportHtml -notmatch [regex]::Escape($marker)) { throw "Missing: $marker" }
        Pass "EX2 UI: $marker"
    }
}

Try-Test "EX2 Sidebar Export nav" {
    if ($exportHtml -notmatch 'href="/Export') { throw "Missing /Export nav link" }
    Pass "EX2 Sidebar nav"
}

# --- QC JSON (before certification) ---
Write-Host ""
Write-Host "--- EX3. Factory QC ---" -ForegroundColor Cyan

Try-Test "EX3 ValidateFactory before cert" {
    $qc = Invoke-RestMethod -Uri "$BaseUrl/Export/ValidateFactory?patternId=$patternId&style=slim" -WebSession $session
    if ($qc.canExportToFactory) { throw "Expected canExportToFactory=false before cert" }
    $blocking = @($qc.issues | Where-Object { $_.code -notin @("NOT_APPROVED", "CUTTER_TEST") })
    if ($blocking.Count -gt 0) {
        $detail = ($blocking | ForEach-Object { $_.code + ": " + $_.message }) -join "; "
        throw "Unexpected blocking QC: $detail"
    }
    Pass "EX3 QC JSON" "geometry OK, not certified"
}

Try-Test "EX3 PreviewPieces API" {
    $pieces = Invoke-RestMethod -Uri "$BaseUrl/Export/PreviewPieces?style=slim" -WebSession $session
    if (@($pieces).Count -lt 3) { throw "Expected piece list" }
    Pass "EX3 PreviewPieces" "$(@($pieces).Count) pieces"
}

Try-Test "EX3 StartExport steps" {
    $steps = Post-Json "$BaseUrl/Export/StartExport" $session @{ format = "DXF" }
    if (@($steps).Count -lt 2) { throw "Expected export steps" }
    Pass "EX3 StartExport" "$(@($steps).Count) steps"
}

# --- Draft exports ---
Write-Host ""
Write-Host "--- EX4. Draft export ---" -ForegroundColor Cyan

$draftDxfBytes = $null
foreach ($fmt in @("DXF", "PLT", "HPGL", "PDF")) {
    Try-Test "EX4 Draft $fmt download" {
        $r = Invoke-WebRequest -Uri (Get-DownloadUrl $patternId "slim" $fmt "draft" "M") -WebSession $session -UseBasicParsing
        if ($r.RawContentLength -lt 50) { throw "File too small ($($r.RawContentLength) bytes)" }
        if ($fmt -eq "DXF") { $script:draftDxfBytes = $r.Content }
        Pass "EX4 Draft $fmt" "$($r.RawContentLength) bytes"
    }
}

Try-Test "EX4 Draft DXF contains canvas/slim_M.dxf" {
    if (-not $draftDxfBytes) { throw "No DXF bytes captured" }
    $handle = Open-ZipBytes $draftDxfBytes
    try {
        $dxf = Read-ZipText $handle.Zip "canvas/slim_M.dxf"
        if (-not $dxf) { throw "canvas/slim_M.dxf missing from draft ZIP" }
        Assert-DxfStructure $dxf "draft slim_M"
        $manifest = Read-ZipText $handle.Zip "manifest.txt"
        if (-not $manifest) { throw "manifest.txt missing from draft DXF ZIP" }
        Pass "EX4 Draft DXF path" "canvas/slim_M.dxf ($($dxf.Length) bytes)"
    }
    finally { Close-ZipHandle $handle }
}

Write-Host ""
Write-Host "--- EX4b. DXF content ---" -ForegroundColor Cyan

Try-Test "EX4b Draft DXF has SA layer" {
    $handle = Open-ZipBytes $draftDxfBytes
    try {
        $dxf = Read-ZipText $handle.Zip "canvas/slim_M.dxf"
        if ($dxf -notmatch "(?m)^LINE\r?\n8\r?\nSA") { throw "Missing SA (seam allowance) layer lines" }
        Pass "EX4b SA layer" "seam allowance present"
    }
    finally { Close-ZipHandle $handle }
}

Try-Test "EX4b Draft DXF all graded sizes" {
    $uri = "$BaseUrl/Export/DownloadPackage?patternId=$patternId&style=slim&format=DXF&purpose=draft&sizes=XS,S,M,L,XL,XXL"
    $r = Invoke-WebRequest -Uri $uri -WebSession $session -UseBasicParsing
    $handle = Open-ZipBytes $r.Content
    try {
        $dxfs = Get-ZipDxfEntries $handle.Zip
        if ($dxfs.Count -ne 6) { throw "Expected 6 DXF files, got $($dxfs.Count)" }
        foreach ($size in @("XS", "S", "M", "L", "XL", "XXL")) {
            $path = "canvas/slim_$size.dxf"
            $text = Read-ZipText $handle.Zip $path
            if (-not $text) { throw "Missing $path" }
            Assert-DxfStructure $text $path
        }
        Pass "EX4b Graded sizes" "6 DXF files in canvas/"
    }
    finally { Close-ZipHandle $handle }
}

Try-Test "EX4 Draft PLT contains cutter commands" {
    $r = Invoke-WebRequest -Uri (Get-DownloadUrl $patternId "slim" "PLT" "draft" "M") -WebSession $session -UseBasicParsing
    $handle = Open-ZipBytes $r.Content
    try {
        $pltEntry = $handle.Zip.Entries | Where-Object { $_.FullName -match "\.plt$" } | Select-Object -First 1
        if (-not $pltEntry) { throw "No .plt in draft ZIP" }
        $stream = $pltEntry.Open()
        $reader = New-Object IO.StreamReader($stream)
        $text = $reader.ReadToEnd()
        $reader.Close()
        if ($text -notmatch "IN;") { throw "PLT missing IN; command" }
        Pass "EX4 Draft PLT" $pltEntry.FullName
    }
    finally { Close-ZipHandle $handle }
}

# --- Factory gate ---
Write-Host ""
Write-Host "--- EX5. Factory certification ---" -ForegroundColor Cyan

Try-Test "EX5 Factory export blocked before cert" {
    try {
        Invoke-WebRequest -Uri (Get-DownloadUrl $patternId "slim" "PLT" "factory" "M") -WebSession $session -UseBasicParsing -ErrorAction Stop | Out-Null
        throw "Factory ZIP should be blocked before certification"
    }
    catch {
        if ($_.Exception.Message -match "Factory ZIP should be blocked") { throw }
        Pass "EX5 Factory blocked" "400 before cert"
    }
}

Try-Test "EX5 Approve for cutting" {
    $resp = Post-Json "$BaseUrl/Export/ApproveForCutting" $session @{
        patternId = $patternId; style = "slim"; actor = "EX QA"
    }
    if (-not $resp.approvedForCutting) { throw "Approve did not set flag" }
    Pass "EX5 Approve" $resp.approvedBy
}

Try-Test "EX5 Record cutter test pass" {
    $resp = Post-Json "$BaseUrl/Export/RecordCutterTest" $session @{
        patternId = $patternId; passed = $true; actor = "EX Factory"; notes = "Export QA automated"
    }
    if (-not $resp.cutterTestPassed) { throw "Cutter test flag not set" }
    Pass "EX5 Cutter test" $resp.cutterTestedBy
}

Try-Test "EX5 ValidateFactory after cert" {
    $qc2 = Invoke-RestMethod -Uri "$BaseUrl/Export/ValidateFactory?patternId=$patternId&style=slim" -WebSession $session
    if (-not $qc2.canExportToFactory) {
        $issues = ($qc2.issues | ForEach-Object { $_.code }) -join ", "
        throw "canExportToFactory=false, issues: $issues"
    }
    Pass "EX5 QC ready" "canExportToFactory=true"
}

Try-Test "EX5 Factory export ZIP" {
    $r = Invoke-WebRequest -Uri (Get-DownloadUrl $patternId "slim" "PLT" "factory" "M") -WebSession $session -UseBasicParsing
    if ($r.RawContentLength -lt 100) { throw "ZIP too small" }
    $handle = Open-ZipBytes $r.Content
    try {
        $cert = $handle.Zip.Entries | Where-Object { $_.Name -eq "certification.json" } | Select-Object -First 1
        if (-not $cert) { throw "certification.json missing from factory ZIP" }
        Pass "EX5 Factory PLT ZIP" "$([math]::Round($r.RawContentLength/1KB, 1)) KB"
    }
    finally { Close-ZipHandle $handle }
}

Try-Test "EX5 Factory DXF export" {
    $r = Invoke-WebRequest -Uri (Get-DownloadUrl $patternId "slim" "DXF" "factory" "M") -WebSession $session -UseBasicParsing
    if ($r.RawContentLength -lt 100) { throw "Factory DXF ZIP too small" }
    $handle = Open-ZipBytes $r.Content
    try {
        $cert = $handle.Zip.Entries | Where-Object { $_.Name -eq "certification.json" } | Select-Object -First 1
        if (-not $cert) { throw "certification.json missing from factory DXF ZIP" }
        $dxf = Read-ZipText $handle.Zip "canvas/slim_M.dxf"
        Assert-DxfStructure $dxf "factory slim_M"
        Pass "EX5 Factory DXF" "canvas/slim_M.dxf + certification.json"
    }
    finally { Close-ZipHandle $handle }
}

# --- CLO + shrinkage ---
Write-Host ""
Write-Host "--- EX6. CLO review and shrinkage ---" -ForegroundColor Cyan

Try-Test "EX6 CLO review DXF" {
    $r = Invoke-WebRequest -Uri (Get-DownloadUrl $patternId "slim" "DXF" "clo" "M") -WebSession $session -UseBasicParsing
    if ($r.RawContentLength -lt 50) { throw "CLO ZIP too small" }
    $handle = Open-ZipBytes $r.Content
    try {
        $dxf = Read-ZipText $handle.Zip "canvas/slim_M.dxf"
        Assert-DxfStructure $dxf "CLO slim_M"
        Pass "EX6 CLO review DXF" "$($r.RawContentLength) bytes"
    }
    finally { Close-ZipHandle $handle }
}

Try-Test "EX6 SetShrinkage" {
    $resp = Post-Json "$BaseUrl/Export/SetShrinkage" $session @{
        patternId = $patternId; percent = 2.5
    }
    $pct = if ($null -ne $resp.shrinkagePercent) { [decimal]$resp.shrinkagePercent } else { [decimal]$resp.ShrinkagePercent }
    if ($pct -ne 2.5) { throw "Expected 2.5%, got $pct" }
    Pass "EX6 Shrinkage" "2.5%"
}

# --- Auth ---
Write-Host ""
Write-Host "--- EX7. Auth ---" -ForegroundColor Cyan

Try-Test "EX7 Export page requires login" {
    try {
        Invoke-WebRequest -Uri "$BaseUrl/Export?patternId=$patternId&style=slim" -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop | Out-Null
        throw "Expected redirect, got 200"
    }
    catch {
        if ($_.Exception.Message -eq "Expected redirect, got 200") { throw }
        Pass "EX7 Auth gate" "redirect to login"
    }
}

# --- Cleanup ---
Write-Host ""
Write-Host "--- EX8. Cleanup ---" -ForegroundColor Cyan

Try-Test "EX8 Delete test pattern" {
    Delete-Pattern $patternId $session
    Pass "EX8 Cleanup" "id=$patternId"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  EXPORT MODULE SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Passed:  $($script:Passed)" -ForegroundColor Green
Write-Host "  Failed:  $($script:Failed)" -ForegroundColor $(if ($script:Failed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($script:Failed -gt 0) {
    Write-Host "Failures:" -ForegroundColor Red
    foreach ($e in $script:Errors) { Write-Host "  - $e" -ForegroundColor Red }
    Write-Host ""
    Write-Host "EXPORT MODULE FAILED." -ForegroundColor Red
    exit 1
}

Write-Host "EXPORT MODULE PASSED." -ForegroundColor Green
Write-Host "Browser-only: format card selection, export progress UI, Preview button." -ForegroundColor DarkGray
exit 0
