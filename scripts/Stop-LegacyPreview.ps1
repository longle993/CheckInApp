$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$resultPath = Join-Path $workspace '.local/nov01/legacy-stop.json'
$results = foreach ($target in @(@{ Port=5082; Id=31164; Name='CheckInApp.Api' }, @{ Port=5175; Id=26540; Name='node' })) {
    $listener = Get-NetTCPConnection -State Listen -LocalPort $target.Port -ErrorAction SilentlyContinue
    if (!$listener -or $listener.OwningProcess -notcontains $target.Id) { @{Port=$target.Port; Status='Already stopped'}; continue }
    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$($target.Id)"
    $name = [IO.Path]::GetFileNameWithoutExtension($process.Name)
    if ($name -ne $target.Name -or (($process.ExecutablePath + ' ' + $process.CommandLine) -notlike "*$workspace*")) { throw "Process identity does not match CheckInApp on port $($target.Port)" }
    Stop-Process -Id $target.Id -ErrorAction Stop
    @{Port=$target.Port; Status='Stopped validated legacy process'}
}
$results | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding utf8
