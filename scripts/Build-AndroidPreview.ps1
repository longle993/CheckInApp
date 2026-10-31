param([string]$ApiUrl)
$ErrorActionPreference = 'Stop'
if (!$ApiUrl) { $ApiUrl = 'http://' + (& "$PSScriptRoot/Get-LanAddress.ps1") + ':5082/api/v1' }
$androidBuildRoot = Split-Path $PSScriptRoot -Parent
$androidOutput = Join-Path $androidBuildRoot '.local/android-preview'
New-Item -ItemType Directory -Force -Path $androidOutput | Out-Null
Push-Location $androidBuildRoot
try {
    # Chỉ xuất phần công khai của các CA đã được Windows tin cậy, không xuất private key.
    $androidCaFolder = Join-Path $androidBuildRoot 'tools/android/.local/certs'
    New-Item -ItemType Directory -Force -Path $androidCaFolder | Out-Null
    foreach ($rootCertificate in (Get-ChildItem Cert:/LocalMachine/Root,Cert:/CurrentUser/Root | Where-Object { $_.NotAfter -gt (Get-Date) } | Sort-Object Thumbprint -Unique)) {
        $pem = "-----BEGIN CERTIFICATE-----`n" + [Convert]::ToBase64String($rootCertificate.RawData,[Base64FormattingOptions]::InsertLineBreaks) + "`n-----END CERTIFICATE-----`n"
        [IO.File]::WriteAllText((Join-Path $androidCaFolder ($rootCertificate.Thumbprint + '.crt')), $pem, [Text.UTF8Encoding]::new($false))
    }
    docker build -t checkinapp-android-builder -f tools/android/Dockerfile tools/android
    if ($LASTEXITCODE -ne 0) { throw 'Android builder image failed.' }
    docker run --rm --name checkinapp-android-build --mount "type=bind,source=$androidBuildRoot/apps/mobile,target=/source,readonly" --mount "type=bind,source=$androidBuildRoot/tools/android,target=/builder,readonly" --mount "type=bind,source=$androidOutput,target=/output" --mount type=volume,source=checkinapp-android-work,target=/work --mount type=volume,source=checkinapp-android-gradle,target=/root/.gradle --mount type=volume,source=checkinapp-android-npm,target=/root/.npm -e "EXPO_PUBLIC_API_URL=$ApiUrl" checkinapp-android-builder bash /builder/build-preview.sh
    if ($LASTEXITCODE -ne 0) { throw 'APK build failed.' }
    @{ apiUrl=$ApiUrl; builtAt=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $androidOutput 'build-context.json') -Encoding utf8
} finally { Pop-Location }
