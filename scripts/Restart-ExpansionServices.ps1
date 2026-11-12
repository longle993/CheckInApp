$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
foreach ($target in @(@{Port=5083;Id=4516;Name='CheckInApp.Api'},@{Port=5175;Id=28080;Name='node'})) {
    $listener = Get-NetTCPConnection -State Listen -LocalPort $target.Port -ErrorAction SilentlyContinue
    if (!$listener -or $listener.OwningProcess -notcontains $target.Id) { continue }
    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$($target.Id)"
    if ([IO.Path]::GetFileNameWithoutExtension($process.Name) -ne $target.Name -or ($process.ExecutablePath+' '+$process.CommandLine) -notlike "*$workspace*") { throw 'Unexpected process identity; stopped without changing services.' }
    Stop-Process -Id $target.Id -ErrorAction Stop
}
'Validated previous CheckInApp services stopped.' | Set-Content -LiteralPath (Join-Path $workspace '.local/expansion-qa/services-stopped.txt')
