# Allow other PCs on the office LAN to connect to PostgreSQL on this PC (PC 1).
# Run PowerShell as Administrator on the machine that hosts PostgreSQL.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools\allow-lan-postgres.ps1
#   powershell -ExecutionPolicy Bypass -File tools\allow-lan-postgres.ps1 -Port 5433

param(
    [int]$Port = 5433,
    [string]$Database = "patternpro",
    [string]$DbUser = "postgres",
    [string]$LanSubnet = "192.168.1.0/24",
    [switch]$ShowIpOnly
)

$ErrorActionPreference = "Stop"

function Get-LanIp {
    $ips = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" } |
        Select-Object -ExpandProperty IPAddress -Unique
    $hostIp = ($ips | Where-Object { $_ -like "192.168.*" } | Select-Object -First 1)
    if (-not $hostIp) { $hostIp = ($ips | Select-Object -First 1) }
    return $hostIp
}

if ($ShowIpOnly) {
    $ip = Get-LanIp
    if ($ip) { Write-Output $ip }
    exit 0
}

function Write-Step([string]$Text) { Write-Host $Text -ForegroundColor Cyan }
function Write-Ok([string]$Text)   { Write-Host "  OK  $Text" -ForegroundColor Green }
function Write-Warn([string]$Text) { Write-Host "  !!  $Text" -ForegroundColor Yellow }

Write-Host ""
Write-Host "=== PatternPro - allow LAN PostgreSQL (PC 1) ===" -ForegroundColor Cyan
Write-Host ""

# --- IP addresses ---
$ips = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" } |
    Select-Object -ExpandProperty IPAddress -Unique

Write-Step "This PC IP (use as Host on other PCs):"
if ($ips) { $ips | ForEach-Object { Write-Ok $_ } }
else { Write-Warn "Run ipconfig to find IPv4 address" }
Write-Step "Windows Firewall - allow inbound TCP $Port ..."
$ruleName = "PatternPro PostgreSQL ($Port)"
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Ok "Rule already exists: $ruleName"
}
else {
    try {
        New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow | Out-Null
        Write-Ok "Created firewall rule: $ruleName"
    }
    catch {
        Write-Warn "Could not create firewall rule. Re-run PowerShell as Administrator."
        Write-Host "       New-NetFirewallRule -DisplayName `"$ruleName`" -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow"
    }
}

# --- Find PostgreSQL data directory ---
Write-Step "PostgreSQL configuration ..."
$dataDirs = @()
foreach ($ver in @("18", "17", "16", "15", "14", "13")) {
    $p = "C:\Program Files\PostgreSQL\$ver\data"
    if (Test-Path $p) { $dataDirs += $p }
}

if ($dataDirs.Count -eq 0) {
    Write-Warn "PostgreSQL data folder not found under C:\Program Files\PostgreSQL\"
}
else {
    foreach ($dataDir in $dataDirs) {
        Write-Host "  Found: $dataDir" -ForegroundColor DarkGray

        $conf = Join-Path $dataDir "postgresql.conf"
        $hba  = Join-Path $dataDir "pg_hba.conf"

        if (Test-Path $conf) {
            $text = Get-Content $conf -Raw
            if ($text -notmatch "(?m)^listen_addresses\s*=\s*'\*'") {
                if ($text -match "(?m)^#?\s*listen_addresses\s*=") {
                    $text = $text -replace "(?m)^#?\s*listen_addresses\s*=.*", "listen_addresses = '*'"
                }
                else {
                    $text += "`nlisten_addresses = '*'`n"
                }
                Set-Content -Path $conf -Value $text -Encoding UTF8
                Write-Ok "postgresql.conf - listen_addresses = '*'"
            }
            else {
                Write-Ok "postgresql.conf already listens on all interfaces"
            }

            if ($text -notmatch "(?m)^port\s*=\s*$Port") {
                if ($text -match "(?m)^#?\s*port\s*=") {
                    $text = Get-Content $conf -Raw
                    $text = $text -replace "(?m)^#?\s*port\s*=.*", "port = $Port"
                    Set-Content -Path $conf -Value $text -Encoding UTF8
                    Write-Ok "postgresql.conf - port = $Port"
                }
            }
        }

        if (Test-Path $hba) {
            $hbaLine = "host    $Database    $DbUser    $LanSubnet    scram-sha-256"
            $hbaText = Get-Content $hba -Raw
            if ($hbaText -notmatch [regex]::Escape($LanSubnet)) {
                Add-Content -Path $hba -Value "`n# PatternPro LAN access`n$hbaLine"
                Write-Ok "pg_hba.conf - added $LanSubnet"
            }
            else {
                Write-Ok "pg_hba.conf already allows $LanSubnet"
            }
        }
    }

    Write-Step "Restarting PostgreSQL services ..."
    Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue |
        Where-Object { $_.Status -eq "Running" } |
        ForEach-Object {
            Restart-Service $_.Name -Force -ErrorAction SilentlyContinue
            Write-Ok "Restarted $($_.Name)"
        }
}

# --- Test local port ---
Write-Step "Testing port $Port on this PC ..."
$local = Test-NetConnection localhost -Port $Port -WarningAction SilentlyContinue
if ($local.TcpTestSucceeded) { Write-Ok "localhost:$Port reachable" }
else { Write-Warn "Port $Port not reachable - check PostgreSQL service" }

# --- DbTool test if repo present ---
$repoRoot = Split-Path $PSScriptRoot -Parent
if (Test-Path (Join-Path $repoRoot "PatternPro.sln")) {
    Write-Step "Database connection test ..."
    Push-Location $repoRoot
    $out = dotnet run --project tools/PatternPro.DbTool -- verify-connection 2>&1
    Pop-Location
    $line = $out | Select-String "OK|FAILED" | Select-Object -Last 1
    if ($line) { Write-Host "  $line" -ForegroundColor Green }
}

Write-Host ""
Write-Step "Other PCs - appsettings.Team.json next to PatternPro.Desktop.exe:"
$hostIp = ($ips | Where-Object { $_ -like "192.168.*" } | Select-Object -First 1)
if (-not $hostIp) { $hostIp = ($ips | Select-Object -First 1) }
if (-not $hostIp) { $hostIp = "192.168.1.15" }
Write-Host ""
Write-Host "{"
Write-Host "  `"ConnectionStrings`": {"
Write-Host "    `"Postgres`": `"Host=$hostIp;Port=$Port;Database=$Database;Username=$DbUser;Password=YOUR_PASSWORD`""
Write-Host "  }"
Write-Host "}"
Write-Host ""
Write-Host "On other PC test:  Test-NetConnection $hostIp -Port $Port" -ForegroundColor DarkGray
Write-Host ""
