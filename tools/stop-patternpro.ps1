# Stop Pattern.Web dev server (frees DLL locks before dotnet build/run)
# Usage: powershell -ExecutionPolicy Bypass -File tools/stop-patternpro.ps1

param([int]$Port = 5001)

$stopped = @()

Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
    ForEach-Object {
        $procId = $_.OwningProcess
        if ($procId -gt 0 -and $stopped -notcontains $procId) {
            Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
            $stopped += $procId
            Write-Host "Stopped process $procId (port $Port)"
        }
    }

Get-Process -Name "Pattern.Web" -ErrorAction SilentlyContinue |
    ForEach-Object {
        if ($stopped -notcontains $_.Id) {
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
            $stopped += $_.Id
            Write-Host "Stopped Pattern.Web $($_.Id)"
        }
    }

if ($stopped.Count -eq 0) {
    Write-Host "No PatternPro dev server found on port $Port"
} else {
    Start-Sleep -Seconds 1
    Write-Host "Done. You can now: dotnet build / dotnet run"
}
