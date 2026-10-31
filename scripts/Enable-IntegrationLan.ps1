param([string]$LanAddress)
$ErrorActionPreference = 'Stop'
if (!$LanAddress) { $LanAddress = & "$PSScriptRoot/Get-LanAddress.ps1" }
$lanInterface = Get-NetIPAddress -AddressFamily IPv4 -IPAddress $LanAddress -ErrorAction Stop
foreach ($port in @(5082,8765)) {
    $ruleName = "CheckInApp-Preview-LAN-$port"
    if (Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue) { Remove-NetFirewallRule -Name $ruleName -ErrorAction Stop }
    New-NetFirewallRule -Name $ruleName -DisplayName "CheckInApp LAN preview $port" -Direction Inbound -Action Allow -Protocol TCP -LocalPort $port -LocalAddress $LanAddress -RemoteAddress LocalSubnet -InterfaceAlias $lanInterface.InterfaceAlias -Profile Any -ErrorAction Stop | Out-Null
}
Write-Output "LAN preview firewall rules enabled for $LanAddress, TCP 5082/8765, LocalSubnet only."
