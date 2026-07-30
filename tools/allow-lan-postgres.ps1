# Run ON THE MAIN PC (Admin PowerShell) so other PCs can use the shared PatternPro database.
# Fixes: FATAL: no pg_hba.conf entry for host "192.168.x.x"
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools\allow-lan-postgres.ps1
#   powershell -ExecutionPolicy Bypass -File tools\allow-lan-postgres.ps1 -LanCidr "192.168.1.0/24"

param(
    [string]$LanCidr = "192.168.1.0/24",
    [string]$DbName = "patternpro",
    [string]$DbUser = "postgres"
)

$ErrorActionPreference = "Stop"

function Find-PostgresDataDir {
    $service = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        Select-Object -First 1
    if ($service) {
        $svc = Get-CimInstance Win32_Service -Filter "Name='$($service.Name)'"
        # Typical: ...\pg_ctl.exe runservice -N "..." -D "C:\Program Files\PostgreSQL\16\data" -w
        if ($svc.PathName -match '-D\s+"([^"]+)"' -or $svc.PathName -match '-D\s+(\S+)') {
            $dir = $Matches[1]
            if (Test-Path (Join-Path $dir "pg_hba.conf")) { return $dir }
        }
    }

    $candidates = @(
        "C:\Program Files\PostgreSQL\16\data",
        "C:\Program Files\PostgreSQL\15\data",
        "C:\Program Files\PostgreSQL\14\data",
        "D:\PostgreSQL\data",
        "E:\PostgreSQL\data"
    )
    foreach ($c in $candidates) {
        if (Test-Path (Join-Path $c "pg_hba.conf")) { return $c }
    }
    return $null
}

$dataDir = Find-PostgresDataDir
if (-not $dataDir) {
    throw "Could not find PostgreSQL data directory. Edit pg_hba.conf manually."
}

$hba = Join-Path $dataDir "pg_hba.conf"
$conf = Join-Path $dataDir "postgresql.conf"
Write-Host "Data dir: $dataDir" -ForegroundColor Cyan

# Ensure Postgres listens on the LAN interface
$confText = Get-Content $conf -Raw
if ($confText -match "(?m)^#?\s*listen_addresses\s*=") {
    $confText = [regex]::Replace($confText, "(?m)^#?\s*listen_addresses\s*=\s*'[^']*'", "listen_addresses = '*'")
} else {
    $confText += "`r`nlisten_addresses = '*'`r`n"
}
Set-Content -Path $conf -Value $confText -Encoding UTF8
Write-Host "Set listen_addresses = '*'" -ForegroundColor Green

$marker = "# PatternPro LAN access ($LanCidr)"
$line = "host    $DbName    $DbUser    $LanCidr    scram-sha-256"
$hbaLines = Get-Content $hba
if ($hbaLines -contains $line -or ($hbaLines -match [regex]::Escape($LanCidr) -and $hbaLines -match $DbName)) {
    Write-Host "pg_hba.conf already has a LAN rule for $LanCidr" -ForegroundColor Yellow
} else {
    Add-Content -Path $hba -Value "`r`n$marker`r`n$line`r`n"
    Write-Host "Added: $line" -ForegroundColor Green
}

$pgService = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending |
    Select-Object -First 1
if (-not $pgService) { throw "PostgreSQL Windows service not found." }

Restart-Service -Name $pgService.Name -Force
Write-Host "Restarted service: $($pgService.Name)" -ForegroundColor Green
Write-Host ""
Write-Host "Other PCs can now use Host=<this-PC-IP>;Port=5433 (or your port);Database=$DbName" -ForegroundColor Cyan
Write-Host "On the other PC run:  tools\use-team-database.ps1" -ForegroundColor Cyan
