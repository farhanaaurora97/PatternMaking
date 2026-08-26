# Build PatternPro Desktop for Windows distribution (folder + ZIP + optional MSIX).
# Usage:  powershell -ExecutionPolicy Bypass -File tools/publish-desktop-windows.ps1

param(
    [switch]$SkipMsix,
    [switch]$SkipLaunchTest,
    [string]$Configuration = "Release",
    [string]$TeamServerHost,
    [int]$PostgresPort = 5433,
    [string]$PostgresPassword = "1234",
    [string]$PostgresDatabase = "patternpro",
    [string]$PostgresUser = "postgres"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "PatternPro.Desktop/PatternPro.Desktop.csproj"
$distRoot = Join-Path $repoRoot "dist"
$folderName = "PatternPro-Desktop-win-x64"
$outDir = Join-Path $distRoot $folderName
$zipPath = Join-Path $distRoot "PatternPro-Desktop-1.0-win-x64.zip"
$tfm = "net8.0-windows10.0.19041.0"
$isTeamPackage = -not [string]::IsNullOrWhiteSpace($TeamServerHost)

function Ensure-WebView2Loader {
    param([string]$Dir)

    $loaderDest = Join-Path $Dir "runtimes\win-x64\native\WebView2Loader.dll"
    $loaderRoot = Join-Path $Dir "WebView2Loader.dll"
    if ((Test-Path $loaderDest) -and (Test-Path $loaderRoot)) {
        return $loaderDest
    }

    $searchRoots = @()
    if (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
        $searchRoots += (Join-Path $env:NUGET_PACKAGES "microsoft.web.webview2")
    }
    $searchRoots += (Join-Path $env:USERPROFILE ".nuget\packages\microsoft.web.webview2")
    # Fallback: already-published dist or obj output
    $searchRoots += (Join-Path $repoRoot "dist")
    $searchRoots += (Join-Path $repoRoot "PatternPro.Desktop")

    $loaderSrc = $null
    foreach ($root in $searchRoots) {
        if (-not (Test-Path $root)) { continue }
        $hit = Get-ChildItem -Path $root -Recurse -Filter WebView2Loader.dll -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '[\\/]win-x64[\\/]native[\\/]' } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($hit) {
            $loaderSrc = $hit.FullName
            break
        }
    }

    if (-not $loaderSrc) {
        throw "WebView2Loader.dll not found. Run: dotnet restore PatternPro.Desktop/PatternPro.Desktop.csproj"
    }

    New-Item -ItemType Directory -Force -Path (Split-Path $loaderDest -Parent) | Out-Null
    Copy-Item $loaderSrc $loaderDest -Force
    Copy-Item $loaderSrc $loaderRoot -Force
    Write-Host "Copied WebView2Loader.dll from $loaderSrc" -ForegroundColor Yellow
    return $loaderDest
}

function Test-PublishedDesktop {
    param([string]$Dir)

    $exe = Join-Path $Dir "PatternPro.Desktop.exe"
    if (-not (Test-Path $exe)) {
        throw "Missing PatternPro.Desktop.exe in $Dir"
    }

    $required = @(
        "Microsoft.Web.WebView2.Core.dll",
        "Microsoft.WindowsAppRuntime.Bootstrap.dll"
    )
    foreach ($file in $required) {
        if (-not (Test-Path (Join-Path $Dir $file))) {
            throw "Missing required file in publish output: $file"
        }
    }

    $loaderPath = Ensure-WebView2Loader -Dir $Dir

    Write-Host "Publish checks OK:" -ForegroundColor Green
    Write-Host "  EXE:     $exe"
    Write-Host "  Loader:  $loaderPath"
}

Write-Host ""
Write-Host "=== PatternPro Desktop - Windows publish ===" -ForegroundColor Cyan
Write-Host "Repo: $repoRoot"
Write-Host ""

Get-Process -Name PatternPro.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

$stagingDir = Join-Path $distRoot "$folderName.build"
if (Test-Path $stagingDir) {
    Remove-Item -Recurse -Force $stagingDir
}
New-Item -ItemType Directory -Force -Path $distRoot | Out-Null

$configName = if ([string]::IsNullOrWhiteSpace($Configuration)) { "Release" } else { $Configuration }
Write-Host "Publishing self-contained app to $stagingDir (config=$configName) ..." -ForegroundColor Yellow
# Argument array avoids PowerShell eating -c/-f/--configuration when values are empty.
$publishArgs = @(
    $project
    "--configuration"; $configName
    "--framework"; $tfm
    "-p:SelfContained=true"
    "-p:PublishSingleFile=false"
    "-p:WindowsPackageType=None"
    "-p:WindowsAppSDKSelfContained=true"
    "-p:RuntimeIdentifierOverride=win10-x64"
    "-o"; $stagingDir
)
& dotnet publish @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Test-PublishedDesktop -Dir $stagingDir

if (Test-Path $outDir) {
    Remove-Item -Recurse -Force $outDir
}
Rename-Item -Path $stagingDir -NewName $folderName

Copy-Item (Join-Path $repoRoot "tools/install-patternpro-desktop.ps1") (Join-Path $outDir "Install-PatternPro.ps1") -Force

function Write-TeamAppSettings {
    param([string]$Dir)

    $userName = if ([string]::IsNullOrWhiteSpace($PostgresUser)) { "postgres" } else { $PostgresUser }
    $dbName = if ([string]::IsNullOrWhiteSpace($PostgresDatabase)) { "patternpro" } else { $PostgresDatabase }
    $pwd = if ($null -eq $PostgresPassword) { "1234" } else { $PostgresPassword }
    $conn = "Host=$TeamServerHost;Port=$PostgresPort;Database=$dbName;Username=$userName;Password=$pwd"
    if ($conn -notmatch 'Username=[^;]+') {
        throw "Team connection string missing Username. Refusing to publish broken package: $conn"
    }
    $settings = @{
        Logging = @{
            LogLevel = @{
                Default = "Information"
                "Microsoft.AspNetCore" = "Warning"
            }
        }
        ConnectionStrings = @{
            Postgres = $conn
        }
        Auth = @{
            SeedAdminUserName = "admin"
            SeedAdminPassword = "Admin@123"
        }
    }
    $json = $settings | ConvertTo-Json -Depth 5
    Set-Content -Path (Join-Path $Dir "appsettings.json") -Value $json -Encoding UTF8
    Set-Content -Path (Join-Path $Dir "appsettings.Team.json") -Value $json -Encoding UTF8

    foreach ($file in @("appsettings.Development.json", "appsettings.Development.example.json",
            "appsettings.Team.example.json", "Setup-Team-Database.ps1")) {
        $path = Join-Path $Dir $file
        if (Test-Path $path) { Remove-Item -Force $path }
    }
}

if ($isTeamPackage) {
    Write-Host "Team package: server $TeamServerHost`:$PostgresPort (no setup needed on other PCs)" -ForegroundColor Yellow
    Write-TeamAppSettings -Dir $outDir
}
else {
    Copy-Item (Join-Path $repoRoot "PatternPro.Desktop/appsettings.Development.example.json") (Join-Path $outDir "appsettings.Development.example.json") -Force
    Copy-Item (Join-Path $repoRoot "PatternPro.Desktop/appsettings.Team.example.json") (Join-Path $outDir "appsettings.Team.example.json") -Force
    Copy-Item (Join-Path $repoRoot "tools/setup-desktop-client.ps1") (Join-Path $outDir "Setup-Team-Database.ps1") -Force

    $distSettings = @{
        Logging = @{
            LogLevel = @{
                Default = "Information"
                "Microsoft.AspNetCore" = "Warning"
            }
        }
        ConnectionStrings = @{
            Postgres = ""
        }
        Auth = @{
            SeedAdminUserName = "admin"
            SeedAdminPassword = "Admin@123"
        }
    }
    $distSettings | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $outDir "appsettings.Development.json") -Encoding UTF8

    $distAppsettings = Join-Path $outDir "appsettings.json"
    $appsettingsObj = Get-Content $distAppsettings -Raw | ConvertFrom-Json
    $appsettingsObj.Auth.SeedAdminPassword = "Admin@123"
    $appsettingsObj.ConnectionStrings.Postgres = ""
    $appsettingsObj | ConvertTo-Json -Depth 5 | Set-Content -Path $distAppsettings -Encoding UTF8
}

$startBat = @(
    "@echo off"
    "cd /d ""%~dp0"""
    "echo Starting PatternPro Desktop..."
    "powershell -NoProfile -ExecutionPolicy Bypass -Command ""Get-ChildItem -File -Recurse | Unblock-File -ErrorAction SilentlyContinue"" 1>nul 2>nul"
    "start """" ""PatternPro.Desktop.exe"""
)
$startBat | Set-Content -Path (Join-Path $outDir "START-PatternPro.bat") -Encoding ASCII

$readme = if ($isTeamPackage) {
    @(
        "PatternPro Desktop - TEAM (shared database)"
        "==========================================="
        ""
        "INSTALL (other PC - nothing else to do):"
        "  1. Extract this ZIP to any folder"
        "  2. Double-click START-PatternPro.bat  (or PatternPro.Desktop.exe)"
        "  3. Login: admin / Admin@123"
        ""
        "You will see the same patterns as the main PC ($TeamServerHost)."
        "No config files to edit. No App_Data to delete."
        ""
        "Requirements:"
        "  - Windows 10/11 64-bit"
        "  - Same office Wi-Fi as the main PC"
        "  - Main PC must be ON (database server)"
        ""
        "If the app does not start:"
        "  - SmartScreen: More info -> Run anyway"
        "  - Check %LocalAppData%\\PatternPro\\startup-error.txt"
        "  - Ask admin for a new ZIP if Wi-Fi/server changed"
        ""
        "Exports save to your Downloads folder."
    )
} else {
    @(
        "PatternPro Desktop 1.0 (Windows x64)"
        "=================================="
        ""
        "QUICK START (other PC):"
        "  1. Copy the full ZIP (use USB or Google Drive if email blocks large files)"
        "  2. Right-click ZIP -> Extract All"
        "  3. Open folder PatternPro-Desktop-win-x64"
        "  4. Double-click START-PatternPro.bat  (or PatternPro.Desktop.exe)"
        "  5. Login: admin / Admin@123"
        ""
        "Requirements:"
        "  - Windows 10/11 64-bit"
        "  - Microsoft Edge WebView2 Runtime (usually already installed)"
        ""
        "If the app does not start:"
        "  - Use START-PatternPro.bat instead of the EXE"
        "  - SmartScreen: More info -> Run anyway"
        "  - Re-download ZIP if extract fails (file may be incomplete)"
        "  - Check %LocalAppData%\\PatternPro\\startup-error.txt"
        ""
        "Optional PostgreSQL (team/shared database on PC 1):"
        "  1. On server PC: run tools/setup-postgres-server.ps1 (from repo)"
        "  2. On this PC: powershell -ExecutionPolicy Bypass -File Setup-Team-Database.ps1"
        "     Or copy appsettings.Team.example.json -> appsettings.Team.json and set Host=SERVER_IP"
        "  See docs/MULTI_PC_SETUP.md in the repo."
        ""
        "Install shortcuts:"
        "  powershell -ExecutionPolicy Bypass -File Install-PatternPro.ps1"
        ""
        "Exports save to your Downloads folder."
    )
}
$readme | Set-Content -Path (Join-Path $outDir "README.txt") -Encoding UTF8

if (-not $SkipLaunchTest) {
    Write-Host ""
    Write-Host "Smoke test: launching PatternPro.Desktop.exe ..." -ForegroundColor Yellow
    $proc = Start-Process -FilePath (Join-Path $outDir "PatternPro.Desktop.exe") -WorkingDirectory $outDir -PassThru
    Start-Sleep -Seconds 4
    if ($proc.HasExited) {
        throw "PatternPro.Desktop.exe exited immediately (exit code $($proc.ExitCode)). Fix publish output before sharing."
    }
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Get-Process -Name PatternPro.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force
    Write-Host "Smoke test OK (process stayed running)." -ForegroundColor Green
}

Get-Process -Name PatternPro.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path $outDir -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "Portable folder: $outDir" -ForegroundColor Green
Write-Host "ZIP package:     $zipPath" -ForegroundColor Green

if (-not $SkipMsix) {
    Write-Host ""
    Write-Host "Building MSIX installer (sideload) ..." -ForegroundColor Yellow
    $certSubject = "CN=PatternPro Dev"
    $cert = Get-ChildItem Cert:\CurrentUser\My -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -eq $certSubject } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if (-not $cert) {
        Write-Host "Creating dev code-signing certificate ($certSubject) ..."
        $cert = New-SelfSignedCertificate `
            -Type CodeSigningCert `
            -Subject $certSubject `
            -KeyUsage DigitalSignature `
            -FriendlyName "PatternPro Desktop Dev" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -KeyExportPolicy Exportable
        $trusted = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPeople", "CurrentUser")
        $trusted.Open("ReadWrite")
        $trusted.Add($cert)
        $trusted.Close()
    }

    $msixDir = Join-Path $distRoot "msix-build"
    if (Test-Path $msixDir) { Remove-Item -Recurse -Force $msixDir }

    $msixArgs = @(
        $project
        "--configuration"; $configName
        "--framework"; $tfm
        "-p:SelfContained=true"
        "-p:CreateWindowsInstaller=true"
        "-p:AppxPackageSigningEnabled=true"
        "-p:PackageCertificateThumbprint=$($cert.Thumbprint)"
        "-p:AppxBundle=Never"
        "-p:RuntimeIdentifierOverride=win10-x64"
        "-o"; $msixDir
    )
    & dotnet publish @msixArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "MSIX build reported errors; checking for output file anyway ..." -ForegroundColor Yellow
    }

    $msixFile = Get-ChildItem -Path (Join-Path $repoRoot "PatternPro.Desktop/bin/$Configuration/$tfm/win10-x64/AppPackages") -Filter *.msix -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($msixFile) {
        $msixDest = Join-Path $distRoot "PatternPro-Desktop-1.0-win-x64.msix"
        Copy-Item $msixFile.FullName $msixDest -Force
        Write-Host "MSIX installer:  $msixDest" -ForegroundColor Green
        Write-Host "Install MSIX: double-click the .msix file. Trust PatternPro Dev cert in certmgr.msc if Windows blocks." -ForegroundColor DarkGray
    }
    elseif ($LASTEXITCODE -ne 0) {
        Write-Host "MSIX build failed (portable ZIP still OK). Re-run with -SkipMsix to hide this step." -ForegroundColor Yellow
    }
    else {
        Write-Host "MSIX publish succeeded but .msix file not found under AppPackages." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Done. Share the ZIP with other Windows PCs." -ForegroundColor Cyan
Write-Host ""
