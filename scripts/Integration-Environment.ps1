$ErrorActionPreference = 'Stop'
$integrationRoot = Split-Path $PSScriptRoot -Parent
$integrationDirectory = Join-Path $integrationRoot '.local/docker-integration'
New-Item -ItemType Directory -Force -Path $integrationDirectory | Out-Null
$integrationSecretsPath = Join-Path $integrationDirectory 'secrets.json'
if (!(Test-Path -LiteralPath $integrationSecretsPath)) {
    function New-IntegrationSecret { 'Ci!9' + [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24)) }
    @{ sqlPassword = New-IntegrationSecret; redisPassword = New-IntegrationSecret; jwtKey = New-IntegrationSecret; email = 'admin@integration.checkin.local'; password = New-IntegrationSecret } |
        ConvertTo-Json | Set-Content -LiteralPath $integrationSecretsPath -Encoding utf8
}
$integrationSecrets = Get-Content -Raw -LiteralPath $integrationSecretsPath | ConvertFrom-Json
$integrationEnvPath = Join-Path $integrationDirectory 'compose.env'
@("SQL_PASSWORD=$($integrationSecrets.sqlPassword)", "REDIS_PASSWORD=$($integrationSecrets.redisPassword)", "JWT_KEY=$($integrationSecrets.jwtKey)", "ADMIN_EMAIL=$($integrationSecrets.email)", "ADMIN_PASSWORD=$($integrationSecrets.password)") |
    Set-Content -LiteralPath $integrationEnvPath -Encoding utf8
$integrationCompose = @('compose', '-p', 'checkinapp-integration', '--env-file', $integrationEnvPath, '-f', (Join-Path $integrationRoot 'compose.yaml'), '-f', (Join-Path $integrationRoot 'compose.integration.yaml'))
$env:ASPNETCORE_ENVIRONMENT = 'Integration'
$env:Database__Provider = 'SqlServer'
$env:ConnectionStrings__Database = "Server=127.0.0.1,14333;Database=CheckInAppIntegration;User Id=sa;Password=$($integrationSecrets.sqlPassword);Encrypt=True;TrustServerCertificate=True"
$env:ConnectionStrings__Redis = "127.0.0.1:16379,password=$($integrationSecrets.redisPassword),abortConnect=false"
$env:Jwt__Key = $integrationSecrets.jwtKey
$env:Bootstrap__AdminEmail = $integrationSecrets.email
$env:Bootstrap__AdminPassword = $integrationSecrets.password
$env:SeedDemoData = 'false'
[Environment]::SetEnvironmentVariable('Logging__LogLevel__Microsoft.EntityFrameworkCore', 'Warning', 'Process')
$env:API_URL = 'http://127.0.0.1:5082/api/v1'
$env:BOOTSTRAP_FILE = $integrationSecretsPath
