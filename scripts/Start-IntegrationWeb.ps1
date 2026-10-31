$ErrorActionPreference = 'Stop'
$env:API_URL = 'http://127.0.0.1:5083'
Push-Location (Split-Path $PSScriptRoot -Parent)
try { npm run dev --prefix apps/web -- --host 127.0.0.1 --port 5174 }
finally { Pop-Location }
