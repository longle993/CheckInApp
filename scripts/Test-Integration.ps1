. "$PSScriptRoot/Integration-Environment.ps1"
Push-Location $integrationRoot
try {
    node tests/docker-integration.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Docker integration tests failed; see .local/docker-integration/results.json.' }
} finally { Pop-Location }
