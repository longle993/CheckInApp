param([switch]$Sql)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
Set-Location $workspace
$qaPort=if($Sql){5096}else{5095}
$qaRoot=Join-Path $workspace $(if($Sql){'.local/ui-expansion-sql'}else{'.local/ui-expansion-test'})
New-Item -ItemType Directory -Force $qaRoot | Out-Null
Copy-Item backend/CheckInApp.Api/appsettings.json (Join-Path $qaRoot 'appsettings.json')
$listener=Get-NetTCPConnection -LocalPort $qaPort -State Listen -ErrorAction SilentlyContinue
if($listener){
    $qaProcess=Get-CimInstance Win32_Process -Filter "ProcessId=$($listener.OwningProcess)"
    if($qaProcess.CommandLine -notlike '*ui-*-build*' -or $qaProcess.CommandLine.Replace('\','/') -notlike "*$($qaRoot.Replace('\','/'))*"){throw 'Port belongs to an unexpected process.'}
    Stop-Process -Id $listener.OwningProcess
}
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:SeedDemoData='true'
[Environment]::SetEnvironmentVariable('Logging__LogLevel__Microsoft.EntityFrameworkCore','Warning','Process')
if($Sql){
    $qaSecrets=Get-Content .local/docker-integration/secrets.json -Raw | ConvertFrom-Json
    $env:Database__Provider='SqlServer'
    $env:ConnectionStrings__Database="Server=127.0.0.1,14333;Database=CheckInAppUiNov02QA;User Id=sa;Password=$($qaSecrets.sqlPassword);Encrypt=True;TrustServerCertificate=True"
    $env:ConnectionStrings__Redis="127.0.0.1:16379,password=$($qaSecrets.redisPassword),abortConnect=false"
}else{
    $env:Database__Provider='Sqlite'
    $env:ConnectionStrings__Database="Data Source=$qaRoot/checkin.db"
    $env:ConnectionStrings__Redis=''
}
dotnet .local/ui-verified-build/CheckInApp.Api.dll --contentRoot $qaRoot --urls "http://127.0.0.1:$qaPort"
