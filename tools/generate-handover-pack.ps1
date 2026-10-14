# Build dist handover folder to send alongside PatternPro-Desktop-1.0-win-x64.zip
# Usage: powershell -ExecutionPolicy Bypass -File tools/generate-handover-pack.ps1
#        powershell -ExecutionPolicy Bypass -File tools/generate-handover-pack.ps1 -AdminContact "Farhana - admin@example.com"

param(
    [string]$AdminContact = "(set name / email / phone)",
    [int]$PostgresPort = 5433,
    [string]$ServerHost
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$distRoot = Join-Path $repoRoot "dist"
$handoverDir = Join-Path $distRoot "PatternPro-Handover-Pack"

if (-not $ServerHost) {
    $allowScript = Join-Path $PSScriptRoot "allow-lan-postgres.ps1"
    if (Test-Path $allowScript) {
        $ServerHost = & $allowScript -ShowIpOnly
    }
}
if (-not $ServerHost) { $ServerHost = "(run publish-team-package.ps1 to detect IP)" }

New-Item -ItemType Directory -Force -Path $handoverDir | Out-Null

$onePager = @"
PatternPro Desktop - HANDOVER (give with the ZIP)
=================================================

SERVER (PC 1 - keep ON for team sync)
  Database host: $ServerHost
  Port:          $PostgresPort
  Database:      patternpro
  ZIP config:    appsettings.Team.json (designers do NOT edit)

DESIGNER PCs
  1. Extract PatternPro-Desktop-1.0-win-x64.zip
  2. Run START-PatternPro.bat or PatternPro.Desktop.exe
  3. Login with YOUR username/password (from administrator - not shared admin)

REQUIREMENTS
  - Windows 10/11 64-bit
  - WebView2 Runtime (usually preinstalled)
  - Same office Wi-Fi or VPN to reach $ServerHost`:$PostgresPort

SUPPORT
  Forgot password -> ask administrator (Admin -> Edit user in app)
  Data not updating -> Refresh in app; confirm PC 1 + PostgreSQL running
  Administrator: $AdminContact

CUSTOMER SMOKE TEST (second PC / VM)
  Login -> Refresh -> Library -> Canvas (M only, Generate) -> Export once

DO NOT SEND
  - appsettings.Development.json from the repo
  - dist/delivery-credentials-*.txt over email (hand passwords securely)

Full guide: MULTI_PC_SETUP.md (included in this folder)
"@

$onePager | Set-Content -Path (Join-Path $handoverDir "HANDOVER.txt") -Encoding UTF8

Copy-Item (Join-Path $repoRoot "docs/MULTI_PC_SETUP.md") (Join-Path $handoverDir "MULTI_PC_SETUP.md") -Force
Copy-Item (Join-Path $repoRoot "docs/DELIVERY_HANDOVER.md") (Join-Path $handoverDir "DELIVERY_HANDOVER.md") -Force

$readmeScreens = Join-Path $handoverDir "SCREENSHOTS-README.txt"
@(
    "Optional training captures (save under docs/screenshots/ in repo):"
    "  dashboard.png   - Dashboard"
    "  size-chart.png  - Size Chart"
    "  canvas.png      - Canvas Editor"
    "  export.png      - Export"
    ""
    "Windows: Win+Shift+S while each screen is open."
) | Set-Content -Path $readmeScreens -Encoding UTF8

$zipHandover = Join-Path $distRoot "PatternPro-Handover-Pack.zip"
if (Test-Path $zipHandover) { Remove-Item -Force $zipHandover }
Compress-Archive -Path (Join-Path $handoverDir "*") -DestinationPath $zipHandover -Force

Write-Host ""
Write-Host "Handover pack written:" -ForegroundColor Green
Write-Host "  Folder: $handoverDir"
Write-Host "  ZIP:    $zipHandover"
Write-Host "  Server: $ServerHost`:$PostgresPort"
Write-Host ""
