# Shared helpers for PatternPro QA scripts (dot-source from tools/*.ps1).

function Post-Json([string]$Uri, $Session, $Object) {
    $json = $Object | ConvertTo-Json -Compress
    return Invoke-RestMethod -Uri $Uri -Method POST -Body $json -ContentType "application/json" -WebSession $Session
}

function Get-SizeChartCsv([string]$BaseUrl, $Session) {
    return (Invoke-WebRequest -Uri "$BaseUrl/SizeChart/ExportCsv" -WebSession $Session -UseBasicParsing).Content
}

function Split-CsvLine([string]$Line) {
    $result = @()
    $current = ""
    $inQuotes = $false
    foreach ($ch in $Line.ToCharArray()) {
        if ($ch -eq '"') {
            $inQuotes = -not $inQuotes
            continue
        }
        if ($ch -eq ',' -and -not $inQuotes) {
            $result += $current
            $current = ""
        }
        else {
            $current += $ch
        }
    }
    $result += $current
    return $result
}

function Get-WaistMValue([string]$Csv) {
    $lines = ($Csv -split "`n") | Where-Object { $_.Trim() -ne "" }
    if ($lines.Count -lt 2) { throw "CSV too short" }

    $headers = Split-CsvLine $lines[0]
    $mIdx = [array]::IndexOf($headers, "M")
    if ($mIdx -lt 0) { throw "M column not found in header: $($lines[0])" }

    $waistLine = $lines | Where-Object { $_ -match "^Waist," } | Select-Object -First 1
    if (-not $waistLine) { throw "Waist row not found in CSV" }

    $parts = Split-CsvLine $waistLine
    if ($parts.Count -le $mIdx) { throw "Waist row too short: $waistLine" }
    return [decimal]$parts[$mIdx]
}

function Ensure-SizeChartWaistM {
    param(
        [Parameter(Mandatory)]
        $Session,
        [string]$BaseUrl = "http://localhost:5001",
        [decimal]$Value = 84,
        [int]$ColumnIndex = 2
    )

    $current = Get-WaistMValue (Get-SizeChartCsv $BaseUrl $Session)
    if ($current -eq $Value) { return }

    Post-Json "$BaseUrl/SizeChart/UpdateCell" $Session @{
        measurementPoint = "Waist"
        columnIndex      = $ColumnIndex
        value            = $Value
    } | Out-Null
}
