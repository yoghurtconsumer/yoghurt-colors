$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-DotNet.ps1')
$installedSdks = & dotnet --list-sdks
$sdkVersion = ($installedSdks | Where-Object { $_ -match '^10\.0\.\d+ ' } | Select-Object -Last 1)
if (-not $sdkVersion) { throw 'Install a current serviced stable .NET 10 SDK for Windows x64 first.' }
$exactVersion = $sdkVersion.Split(' ')[0]
@{ sdk = @{ version = $exactVersion; rollForward = 'disable'; allowPrerelease = $false } } | ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot '../global.json')
Write-Host "Pinned installed SDK $exactVersion. Run restore, review and commit package lock files."
