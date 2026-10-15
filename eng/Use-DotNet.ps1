# Dot-source this file to select the user-local SDK in the current PowerShell only.
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$chromaticDotnetDirectory = Join-Path $env:LOCALAPPDATA 'ChromaticDesktop/dotnet'
if (Test-Path (Join-Path $chromaticDotnetDirectory 'dotnet.exe')) {
    $env:DOTNET_ROOT = $chromaticDotnetDirectory
    $env:DOTNET_ROOT_X64 = $chromaticDotnetDirectory
    # Presence is insufficient: a legacy SDK may appear earlier in Explorer's PATH.
    $chromaticRemainingPath = ($env:PATH -split ';' | Where-Object { $_ -ne $chromaticDotnetDirectory }) -join ';'
    $env:PATH = $chromaticDotnetDirectory + ';' + $chromaticRemainingPath
}
