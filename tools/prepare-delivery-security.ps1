# Reset app-user passwords for delivery; update local seed config; write credentials file (gitignored dist/).
# Does NOT auto-approve disabled users (review list-users output).
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools/prepare-delivery-security.ps1
#   powershell -ExecutionPolicy Bypass -File tools/prepare-delivery-security.ps1 -PostgresPassword 'YourNewDbPassword'

param(
    [string]$CredentialsFile,
    [string]$PostgresPassword,
    [switch]$SkipPostgresPasswordUpdate
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$dbTool = "dotnet run --project tools/PatternPro.DbTool --"
$devSettings = Join-Path $repoRoot "Pattern.Web/appsettings.Development.json"

function New-RandomPassword {
    param([int]$Length = 16)
    $chars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#%*-_"
    $bytes = New-Object byte[] ($Length)
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    -join ($bytes | ForEach-Object { $chars[$_ % $chars.Length] })
}

if (-not $CredentialsFile) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmm"
    $CredentialsFile = Join-Path $repoRoot "dist/delivery-credentials-$stamp.txt"
}

New-Item -ItemType Directory -Force -Path (Split-Path $CredentialsFile -Parent) | Out-Null

Write-Host ""
Write-Host "=== PatternPro delivery security ===" -ForegroundColor Cyan
Write-Host ""

Push-Location $repoRoot
$listOut = dotnet run --project tools/PatternPro.DbTool -- list-users 2>&1
Pop-Location
$listText = ($listOut | ForEach-Object { "$_" }) -join "`n"
Write-Host $listText

$users = @()
foreach ($line in ($listText -split "`n")) {
    if ($line -notmatch "user=([^\t]+).*active=(True|False).*role=([^\t]+)") { continue }
    $users += [pscustomobject]@{
        UserName = $Matches[1]
        IsActive = ($Matches[2] -eq "True")
        Role     = $Matches[3]
    }
}

if ($users.Count -eq 0) { throw "No users parsed from list-users." }

$adminPw = New-RandomPassword -Length 18
$credLines = @(
    "PatternPro delivery credentials - CONFIDENTIAL - do not commit or email in plain text.",
    "Generated: $(Get-Date -Format o)",
    "",
    "=== Application logins (share each person their row only) ==="
)

Push-Location $repoRoot
foreach ($u in $users) {
    if (-not $u.IsActive) {
        $credLines += "$($u.UserName) ($($u.Role)): DISABLED - password unchanged; approve in Admin if they should work."
        continue
    }

    $pw = if ($u.UserName -eq "admin") { $adminPw } else { New-RandomPassword -Length 16 }
    $out = dotnet run --project tools/PatternPro.DbTool -- set-password $u.UserName $pw 2>&1
    $outText = ($out | ForEach-Object { "$_" }) -join "`n"
    if ($LASTEXITCODE -ne 0 -or $outText -notmatch "Set password for") {
        throw "set-password failed for $($u.UserName): $outText"
    }

    $credLines += "$($u.UserName) ($($u.Role)): $pw"
}

$verify = dotnet run --project tools/PatternPro.DbTool -- verify-login admin $adminPw 2>&1
$verifyText = ($verify | ForEach-Object { "$_" }) -join "`n"
if ($verifyText -notmatch "ValidateLogin\('admin'\): OK") {
    throw "Admin verify-login failed after reset: $verifyText"
}
Pop-Location

$credLines += ""
$credLines += "=== Disabled accounts (unchanged) ==="
foreach ($u in ($users | Where-Object { -not $_.IsActive })) {
    $credLines += "  $($u.UserName) ($($u.Role))"
}
if (-not ($users | Where-Object { -not $_.IsActive })) {
    $credLines += "  (none)"
}

$credLines += ""
$credLines += "=== Local dev only (Pattern.Web/appsettings.Development.json) ==="
$credLines += "  Auth:SeedAdminPassword updated to match admin (for DbTool seed on empty DB)."
$credLines += "  Team ZIP must use appsettings.Team.json from publish-team-package.ps1 - never copy Development.json."

if (Test-Path $devSettings) {
    $json = Get-Content $devSettings -Raw | ConvertFrom-Json
    if (-not $json.Auth) { $json | Add-Member -NotePropertyName Auth -NotePropertyValue ([pscustomobject]@{}) }
    $json.Auth.SeedAdminPassword = $adminPw
    if (-not $SkipPostgresPasswordUpdate -and $PostgresPassword) {
        $json.ConnectionStrings.Postgres = "Host=localhost;Port=5433;Database=patternpro;Username=postgres;Password=$PostgresPassword"
        $credLines += "  Postgres password updated in appsettings.Development.json (also set on PostgreSQL server)."
    }
    $json | ConvertTo-Json -Depth 5 | Set-Content -Path $devSettings -Encoding UTF8
}
else {
    $credLines += "  (skipped - no appsettings.Development.json)"
}

$credLines += ""
$credLines += "=== Team package publish ==="
$credLines += "  powershell -ExecutionPolicy Bypass -File tools/publish-team-package.ps1 -PostgresPassword '<server-db-password>'"
$credLines += "  ZIP contains appsettings.Team.json only (no appsettings.Development.json)."

$credLines | Set-Content -Path $CredentialsFile -Encoding UTF8

Write-Host ""
Write-Host "Credentials written to:" -ForegroundColor Green
Write-Host "  $CredentialsFile"
Write-Host ""
Write-Host "Admin login verified. Disabled users were NOT approved automatically." -ForegroundColor Yellow
Write-Host ""
