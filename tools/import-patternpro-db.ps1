# Run ON THIS (other) PC after copying a dump from the main PC.
# Restores into local PostgreSQL and points the app at localhost.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools\import-patternpro-db.ps1 -DumpFile "D:\USB\PatternPro\patternpro-....dump"

param(
    [Parameter(Mandatory = $true)]
    [string]$DumpFile,
    [string]$HostName = "localhost",
    [int]$Port = 5432,
    [string]$Database = "patternpro",
    [string]$Username = "postgres",
    [string]$Password = "1234"
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $DumpFile)) { throw "Dump not found: $DumpFile" }

$psql = @(
    "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    "C:\Program Files\PostgreSQL\15\bin\psql.exe",
    "C:\Program Files\PostgreSQL\14\bin\psql.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
$pgRestore = @(
    "C:\Program Files\PostgreSQL\16\bin\pg_restore.exe",
    "C:\Program Files\PostgreSQL\15\bin\pg_restore.exe",
    "C:\Program Files\PostgreSQL\14\bin\pg_restore.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $psql -or -not $pgRestore) { throw "psql/pg_restore not found." }

$env:PGPASSWORD = $Password
Write-Host "Ensuring database $Database exists ..." -ForegroundColor Cyan
& $psql -h $HostName -p $Port -U $Username -d postgres -v ON_ERROR_STOP=1 -c "SELECT 1 FROM pg_database WHERE datname='$Database';" | Out-Null
$exists = & $psql -h $HostName -p $Port -U $Username -d postgres -t -A -c "SELECT 1 FROM pg_database WHERE datname='$Database';"
if (-not $exists) {
    & $psql -h $HostName -p $Port -U $Username -d postgres -c "CREATE DATABASE $Database;"
}

Write-Host "Restoring $DumpFile ..." -ForegroundColor Cyan
& $pgRestore -h $HostName -p $Port -U $Username -d $Database --clean --if-exists --no-owner --no-acl $DumpFile
# pg_restore may return non-zero for benign warnings; verify count instead
$count = & $psql -h $HostName -p $Port -U $Username -d $Database -t -A -c 'SELECT COUNT(*) FROM patternpro.patterns;'
Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue

Write-Host "Restored pattern count: $count" -ForegroundColor Green

$repoRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot "use-team-database.ps1") -HostIp $HostName -Port $Port -Database $Database -Username $Username -Password $Password

Write-Host ""
Write-Host "Import complete. Restart PatternPro — you should see ~$count patterns." -ForegroundColor Green
