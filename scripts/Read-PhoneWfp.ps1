param([string]$PhoneIp='192.168.0.105')
$ErrorActionPreference='Stop'
$folder=Join-Path (Split-Path $PSScriptRoot -Parent) '.local/network-diagnostic'
& netsh wfp show options optionsfor=NETEVENTS | Out-File (Join-Path $folder 'wfp-options.txt')
& netsh wfp show netevents ("file="+(Join-Path $folder 'wfp-events.xml')) ("remoteaddr="+$PhoneIp) timewindow=900 | Out-File (Join-Path $folder 'wfp-read-status.txt')
& netsh wfp show filters ("file="+(Join-Path $folder 'wfp-filters.xml')) protocol=6 localport=8765 ("remoteaddr="+$PhoneIp) | Out-File (Join-Path $folder 'wfp-read-status.txt') -Append
& netsh wfp show state ("file="+(Join-Path $folder 'wfp-state.xml')) | Out-File (Join-Path $folder 'wfp-read-status.txt') -Append
Get-WinEvent -FilterHashtable @{LogName='Security';Id=5152,5157;StartTime=(Get-Date).AddMinutes(-15)} -ErrorAction SilentlyContinue | Where-Object {$_.Message -match [regex]::Escape($PhoneIp)} | Select-Object -First 30 TimeCreated,Id,Message | ConvertTo-Json -Depth 4 | Out-File (Join-Path $folder 'wfp-security-events.json')
Set-Content (Join-Path $folder 'wfp-read-done.txt') 'Read-only WFP diagnostics finished.'
