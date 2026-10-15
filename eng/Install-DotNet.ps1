param([string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'ChromaticDesktop/dotnet'))
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$metadataUri = 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
$metadata = Invoke-RestMethod -Uri $metadataUri
$version = $metadata.'latest-sdk'
if ($version -notmatch '^10\.0\.\d+$') { throw 'Microsoft metadata did not return a stable .NET 10 SDK.' }
$release = $metadata.releases | Where-Object { $_.sdk.version -eq $version } | Select-Object -First 1
$archive = $release.sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -eq 'dotnet-sdk-win-x64.zip' } | Select-Object -First 1
if (-not $archive -or $archive.url -notlike 'https://builds.dotnet.microsoft.com/dotnet/Sdk/*' -or $archive.hash -notmatch '^[a-fA-F0-9]{128}$') {
    throw 'Missing or unexpected Microsoft Windows x64 SDK archive metadata.'
}
$destination = [IO.Path]::GetFullPath($InstallDirectory)
if (Test-Path (Join-Path $destination "sdk/$version")) {
    Write-Host "SDK $version is already installed in $destination."
    & (Join-Path $destination 'dotnet.exe') --list-sdks
    exit $LASTEXITCODE
}
$downloadPath = Join-Path ([IO.Path]::GetTempPath()) ("chromatic-dotnet-" + [Guid]::NewGuid().ToString('N') + '.zip')
try {
    Write-Host "Downloading Microsoft .NET SDK $version (Windows x64)..."
    Invoke-WebRequest -Uri $archive.url -UseBasicParsing -OutFile $downloadPath
    $actualHash = (Get-FileHash -LiteralPath $downloadPath -Algorithm SHA512).Hash
    if ($actualHash -ne $archive.hash) { throw 'SDK archive SHA-512 does not match Microsoft release metadata.' }
    Write-Host "SHA-512 verified. Extracting into $destination..."
    New-Item -ItemType Directory -Force $destination | Out-Null
    Expand-Archive -LiteralPath $downloadPath -DestinationPath $destination -Force
    & (Join-Path $destination 'dotnet.exe') --list-sdks
    if ($LASTEXITCODE -ne 0) { throw 'Installed SDK host verification failed.' }
    Write-Host 'Installed. Use . ./eng/Use-DotNet.ps1 in PowerShell, or ./eng/dotnet.ps1 <command>.'
}
finally { if (Test-Path -LiteralPath $downloadPath) { Remove-Item -LiteralPath $downloadPath } }
