# Build PatternPro Desktop for Windows distribution (folder + ZIP + optional MSIX).
# Usage:  powershell -ExecutionPolicy Bypass -File tools/publish-desktop-windows.ps1

param(
    [switch]$SkipMsix,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "PatternPro.Desktop/PatternPro.Desktop.csproj"
$distRoot = Join-Path $repoRoot "dist"
$folderName = "PatternPro-Desktop-win-x64"
$outDir = Join-Path $distRoot $folderName
$zipPath = Join-Path $distRoot "PatternPro-Desktop-1.0-win-x64.zip"
$tfm = "net8.0-windows10.0.19041.0"

Write-Host ""
Write-Host "=== PatternPro Desktop - Windows publish ===" -ForegroundColor Cyan
Write-Host "Repo: $repoRoot"
Write-Host ""

Get-Process -Name PatternPro.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path $outDir) {
    Remove-Item -Recurse -Force $outDir
}
New-Item -ItemType Directory -Force -Path $distRoot | Out-Null

Write-Host "Publishing self-contained app to $outDir ..." -ForegroundColor Yellow
dotnet publish $project `
    -c $Configuration `
    -f $tfm `
    -p:SelfContained=true `
    -p:PublishSingleFile=false `
    -o $outDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Copy-Item (Join-Path $repoRoot "tools/install-patternpro-desktop.ps1") (Join-Path $outDir "Install-PatternPro.ps1") -Force
Copy-Item (Join-Path $repoRoot "PatternPro.Desktop/appsettings.Development.example.json") (Join-Path $outDir "appsettings.Development.example.json") -Force

$readme = @(
    "PatternPro Desktop 1.0 (Windows x64)"
    "=================================="
    ""
    "Run (portable):"
    "  PatternPro.Desktop.exe"
    ""
    "Install to your PC (Start menu + desktop shortcut):"
    "  powershell -ExecutionPolicy Bypass -File Install-PatternPro.ps1"
    ""
    "Database:"
    "  Copy appsettings.Development.example.json to appsettings.Development.json"
    "  and set ConnectionStrings:Postgres, or configure appsettings.json before distributing."
    ""
    "Exports save to your Downloads folder."
)
$readme | Set-Content -Path (Join-Path $outDir "README.txt") -Encoding UTF8

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

    dotnet publish $project `
        -c $Configuration `
        -f $tfm `
        -p:SelfContained=true `
        -p:CreateWindowsInstaller=true `
        -p:AppxPackageSigningEnabled=true `
        -p:PackageCertificateThumbprint=$($cert.Thumbprint) `
        -p:AppxBundle=Never `
        -o $msixDir

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
Write-Host "Done. Share the ZIP or run Install-PatternPro.ps1 from the published folder." -ForegroundColor Cyan
Write-Host ""
