$ErrorActionPreference = 'Stop'
$compiler = Get-Command cl.exe -ErrorAction SilentlyContinue
if (-not $compiler) { throw 'ABI qualification needs MSVC C++ Build Tools plus Windows 10/11 SDK headers in an x64 Developer PowerShell. This compile-only check never calls display APIs.' }
$destination = Join-Path $PSScriptRoot '../artifacts/native-abi'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$source = Join-Path $PSScriptRoot 'native/DisplayAbi.cpp'
& $compiler.Source /nologo /std:c++17 /W4 /WX /c $source "/Fo$destination/DisplayAbi.obj"
if ($LASTEXITCODE -ne 0) { throw 'Windows SDK native ABI assertions failed; mode backend remains unqualified.' }
Write-Host 'Windows SDK x64 ABI compile-only assertions passed. No display API was called.'
