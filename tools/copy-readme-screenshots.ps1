# Copy Cypress full-page captures into docs/screenshots/ for README.md
# Prerequisite: Pattern.Web on http://localhost:5001, then npm run cy:screenshots --prefix e2e

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$src = Join-Path $root "e2e\cypress\screenshots\screenshots.cy.js"
$dest = Join-Path $root "docs\screenshots"

if (-not (Test-Path $src)) {
    Write-Error "Run Cypress first: npm run cy:screenshots --prefix e2e"
}

$map = @{
    "02-dashboard.png"  = "dashboard.png"
    "07-size-chart.png" = "size-chart.png"
    "05-canvas.png"     = "canvas.png"
    "08-export.png"     = "export.png"
}

foreach ($k in $map.Keys) {
    Copy-Item (Join-Path $src $k) (Join-Path $dest $map[$k]) -Force
}

Write-Host "Updated README screenshots in docs/screenshots/" -ForegroundColor Green
