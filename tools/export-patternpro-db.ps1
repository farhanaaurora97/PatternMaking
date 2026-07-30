# Run ON THE MAIN PC — creates a USB/file dump of all patterns for another PC.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools\export-patternpro-db.ps1
#   powershell -ExecutionPolicy Bypass -File tools\export-patternpro-db.ps1 -OutDir "D:\USB\PatternPro"

param(
    [string]$OutDir = "",
    [string]$HostName = "localhost",
    [int]$Port = 5433,
    [string]$Database = "patternpro",
    [string]$Username = "postgres",
    [string]$Password = "1234"
)

$ErrorActionPreference = "Stop"
if (-not $OutDir) {
    $OutDir = Join-Path ([Environment]::GetFolderPath("Desktop")) "PatternPro-DB-Export"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$pgDump = @(
    "C:\Program Files\PostgreSQL\16\bin\pg_dump.exe",
    "C:\Program Files\PostgreSQL\15\bin\pg_dump.exe",
    "C:\Program Files\PostgreSQL\14\bin\pg_dump.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $pgDump) { throw "pg_dump.exe not found. Install PostgreSQL client tools on the main PC." }

$stamp = Get-Date -Format "yyyyMMdd-HHmm"
$dumpFile = Join-Path $OutDir "patternpro-$stamp.dump"
$env:PGPASSWORD = $Password
& $pgDump -h $HostName -p $Port -U $Username -d $Database -Fc -f $dumpFile
Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
if ($LASTEXITCODE -ne 0) { throw "pg_dump failed." }

# Also copy JSON App_Data if present (fallback for JSON-only installs)
$repoAppData = Join-Path (Split-Path -Parent $PSScriptRoot) "Pattern.Web\App_Data"
if (Test-Path $repoAppData) {
    $appDataOut = Join-Path $OutDir "App_Data"
    if (Test-Path $appDataOut) { Remove-Item -Recurse -Force $appDataOut }
    Copy-Item -Recurse $repoAppData $appDataOut
    Write-Host "Copied App_Data -> $appDataOut" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Export ready:" -ForegroundColor Green
Write-Host "  $dumpFile"
Write-Host "Copy the whole folder to the other PC, then run:" -ForegroundColor Cyan
Write-Host "  tools\import-patternpro-db.ps1 -DumpFile `"$dumpFile`""
