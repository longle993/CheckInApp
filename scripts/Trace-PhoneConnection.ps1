param([string]$PhoneIp='192.168.0.105',[int]$Seconds=45)
$ErrorActionPreference='Stop'
$traceRoot=Join-Path (Split-Path $PSScriptRoot -Parent) '.local/network-diagnostic'
New-Item -ItemType Directory -Force -Path $traceRoot | Out-Null
$traceFolder=Join-Path $traceRoot ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force -Path $traceFolder | Out-Null
Set-Content -LiteralPath (Join-Path $traceRoot 'latest.txt') -Value $traceFolder
$started=$false
try {
    $status=& pktmon status 2>&1
    $status | Out-File (Join-Path $traceFolder 'initial-status.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Cannot access Packet Monitor.' }
    if (($status -join "`n") -notmatch '(?i)not running|stopped') { throw 'An existing capture may be running; leaving it unchanged.' }
    $filters=& pktmon filter list 2>&1
    $filters | Out-File (Join-Path $traceFolder 'initial-filters.txt')
    if (($filters -join "`n") -notmatch '(?i)none|no filters|empty') { throw 'Existing packet filters detected; leaving them unchanged.' }
    & pktmon filter add CheckInAppPhoneDownload -i $PhoneIp -t TCP SYN -p 8765 | Out-File (Join-Path $traceFolder 'setup.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Cannot add download diagnostic filter.' }
    & pktmon filter add CheckInAppPhoneApi -i $PhoneIp -t TCP SYN -p 5082 | Out-File (Join-Path $traceFolder 'setup.txt') -Append
    if ($LASTEXITCODE -ne 0) { throw 'Cannot add API diagnostic filter.' }
    & pktmon start --capture --pkt-size 64 --file-size 8 --file-name (Join-Path $traceFolder 'phone.etl') | Out-File (Join-Path $traceFolder 'setup.txt') -Append
    if ($LASTEXITCODE -ne 0) { throw 'Cannot start diagnostic capture.' }
    $started=$true
    Set-Content (Join-Path $traceFolder 'ready.txt') 'Capture ready: SYN headers only, phone TCP 8765/5082.'
    Start-Sleep -Seconds $Seconds
} catch {
    $_.Exception.Message | Out-File (Join-Path $traceFolder 'error.txt')
} finally {
    if ($started) {
        & pktmon counters | Out-File (Join-Path $traceFolder 'counters.txt')
        & pktmon stop | Out-File (Join-Path $traceFolder 'stop.txt')
        & pktmon etl2txt (Join-Path $traceFolder 'phone.etl') --out (Join-Path $traceFolder 'phone.txt') --verbose | Out-File (Join-Path $traceFolder 'convert.txt')
    }
    Set-Content (Join-Path $traceFolder 'done.txt') 'Diagnostic finished.'
}
