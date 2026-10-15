. (Join-Path $PSScriptRoot 'Use-DotNet.ps1')
& dotnet @args
exit $LASTEXITCODE
