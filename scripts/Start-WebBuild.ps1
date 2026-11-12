param([switch]$Rebuild, [string]$ApiUrl = "http://127.0.0.1:5083")
$ErrorActionPreference = 'Stop'
$env:API_URL = $ApiUrl
$webBuildRoot = Split-Path $PSScriptRoot -Parent
Push-Location $webBuildRoot
try {
    if ($Rebuild -or !(Test-Path 'apps/web/dist/index.html')) {
        npm run build --prefix apps/web
        if ($LASTEXITCODE -ne 0) { throw 'Web build failed.' }
    }
    npm run preview --prefix apps/web -- --host 127.0.0.1 --port 5175 --strictPort
} finally { Pop-Location }
