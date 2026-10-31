param([string]$LanAddress)
. "$PSScriptRoot/Integration-Environment.ps1"
$integrationUrls = 'http://127.0.0.1:5082'
if ($LanAddress) {
    $parsedLanAddress = $null
    if (![System.Net.IPAddress]::TryParse($LanAddress, [ref]$parsedLanAddress) -or $parsedLanAddress.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork) { throw 'LanAddress must be an IPv4 address of this computer.' }
    $integrationUrls += ";http://${LanAddress}:5082"
}
& docker @integrationCompose up -d --wait --wait-timeout 240 sqlserver redis
if ($LASTEXITCODE -ne 0) { throw 'Docker integration services did not become healthy.' }
Push-Location $integrationRoot
try {
    dotnet build CheckInApp.slnx --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    # Integration sử dụng nhánh MigrateAsync của API, không dùng EnsureCreated.
    dotnet run --project backend/CheckInApp.Api --configuration Release --no-build --no-launch-profile --urls $integrationUrls
} finally { Pop-Location }
