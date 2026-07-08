# Shared helpers for PatternPro QA scripts (dot-source from tools/*.ps1).

function Post-Json([string]$Uri, $Session, $Object) {
    $json = $Object | ConvertTo-Json -Compress
    return Invoke-RestMethod -Uri $Uri -Method POST -Body $json -ContentType "application/json" -WebSession $Session
}

function Get-SizeChartCsv([string]$BaseUrl, $Session) {
    return (Invoke-WebRequest -Uri "$BaseUrl/SizeChart/ExportCsv" -WebSession $Session -UseBasicParsing).Content
}

function Get-WaistMValue([string]$Csv) {
    $waistLine = ($Csv -split "`n") | Where-Object { $_ -match "^Waist," } | Select-Object -First 1
    if (-not $waistLine) { throw "Waist row not found in CSV" }
    $parts = $waistLine -split ","
    if ($parts.Count -lt 4) { throw "Waist row too short: $waistLine" }
    return [decimal]$parts[3]
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
