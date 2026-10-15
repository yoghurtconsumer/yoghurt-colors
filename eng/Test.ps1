param([switch]$Locked)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-DotNet.ps1')
$repoDirectory = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repoDirectory
try {
    $restoreArguments = @('restore', 'ChromaticDesktop.sln', '-p:Platform=x64')
    if ($Locked) { $restoreArguments += '--locked-mode' }
    & dotnet @restoreArguments
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & dotnet build ChromaticDesktop.sln --no-restore --configuration Release -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & dotnet test ChromaticDesktop.sln --no-build --no-restore --configuration Release -p:Platform=x64 --logger trx --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed. See artifacts/test-results.' }
}
finally { Pop-Location }
