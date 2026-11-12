$lanCandidates = foreach ($adapter in [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()) {
    if ($adapter.OperationalStatus -ne 'Up') { continue }
    $properties = $adapter.GetIPProperties()
    if (!($properties.GatewayAddresses | Where-Object { $_.Address.AddressFamily -eq 'InterNetwork' -and $_.Address.ToString() -ne '0.0.0.0' })) { continue }
    foreach ($address in $properties.UnicastAddresses) {
        if ($address.Address.AddressFamily -eq 'InterNetwork' -and $address.Address.ToString() -notlike '169.254*') {
            [pscustomobject]@{ Address = $address.Address.ToString(); Priority = $(if ($adapter.NetworkInterfaceType -eq 'Wireless80211') { 0 } else { 1 }) }
        }
    }
}
$selectedLan = $lanCandidates | Sort-Object Priority | Select-Object -First 1
if (!$selectedLan) { throw 'No LAN IPv4 address found. Pass the address explicitly.' }
$selectedLan.Address
