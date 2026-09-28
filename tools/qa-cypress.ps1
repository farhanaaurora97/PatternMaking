# Pattern.Web Cypress E2E — requires http://localhost:5001
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$e2e = Join-Path $root 'e2e'

try {
  $null = Invoke-WebRequest -Uri 'http://localhost:5001/Account/Login' -UseBasicParsing -TimeoutSec 5
} catch {
  Write-Error 'Pattern.Web is not running. Start: dotnet run --project Pattern.Web --urls http://localhost:5001'
}

Push-Location $e2e
try {
  if (-not (Test-Path 'node_modules\cypress')) {
    npm install
  }
  npm run cy:run
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally {
  Pop-Location
}
