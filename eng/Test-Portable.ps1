param([switch]$SkipShell)
$ErrorActionPreference = 'Stop'
$repoPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$probePath = Join-Path $repoPath '.stage1-probe'
New-Item -ItemType Directory -Force $probePath | Out-Null
$sdkLines = & dotnet --list-sdks
$sdkVersion = ($sdkLines | Where-Object { $_ -match '^8\.0\.' } | Select-Object -Last 1).Split(' ')[0]
if (-not $sdkVersion) { throw 'The optional source probe requires an installed .NET 8 SDK. Production still requires .NET 10.' }
@{ sdk = @{ version = $sdkVersion; rollForward = 'disable'; allowPrerelease = $false } } | ConvertTo-Json | Set-Content (Join-Path $probePath 'global.json')
'<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><LangVersion>12</LangVersion><DefineConstants>$(DefineConstants);CHROMATIC_PORTABLE_PROBE</DefineConstants><TreatWarningsAsErrors>true</TreatWarningsAsErrors><EnableDefaultCompileItems>false</EnableDefaultCompileItems><PlatformTarget>x64</PlatformTarget><RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources></PropertyGroup></Project>' | Set-Content (Join-Path $probePath 'Directory.Build.props')
foreach ($subdirectory in @('foundation', 'tests')) { New-Item -ItemType Directory -Force (Join-Path $probePath $subdirectory) | Out-Null }
@'
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <Compile Include="../../src/Chromatic.Core/**/*.cs;../../src/Chromatic.Infrastructure/Persistence/**/*.cs;../../src/Chromatic.Infrastructure/Ipc/Protocol.cs;../../src/Chromatic.Infrastructure/Ipc/PipeAuthorization.cs" Exclude="../../src/**/obj/**;../../src/**/bin/**" />
    <EmbeddedResource Include="../../docs/schemas/*.schema.json" LogicalName="Chromatic.Schemas.%(Filename)%(Extension)" />
  </ItemGroup>
</Project>
'@ | Set-Content (Join-Path $probePath 'foundation/Foundation.csproj')
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><IsPackable>false</IsPackable></PropertyGroup>
  <ItemGroup><ProjectReference Include="../foundation/Foundation.csproj" /></ItemGroup>
  <ItemGroup><Compile Include="../../tests/Chromatic.Core.Tests/*.cs" /><Compile Include="../../tests/Chromatic.Integration.Tests/PersistenceTests.cs;../../tests/Chromatic.Integration.Tests/IpcTests.cs;../../tests/Chromatic.Integration.Tests/TestDirectory.cs" /><Compile Include="../../eng/SourceProbeRunner.cs" /></ItemGroup>
  <ItemGroup><PackageReference Include="xunit" /></ItemGroup>
</Project>
'@ | Set-Content (Join-Path $probePath 'tests/Tests.csproj')
Push-Location $probePath
$previousTestRoot = $env:CHROMATIC_TEST_ROOT
$env:CHROMATIC_TEST_ROOT = Join-Path $probePath 'test-data'
try {
    Write-Host 'ISOLATED .NET 8 SOURCE PROBE. Does not build/qualify the .NET 10 product or mutate hardware.'
    New-Item -ItemType Directory -Force (Join-Path $repoPath 'artifacts') | Out-Null
    # Offline cached packages only; this probe cannot perform a network vulnerability audit.
    & dotnet build tests/Tests.csproj -p:NuGetAudit=false -p:BuildInParallel=false -p:UseSharedCompilation=false -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Foundation probe build failed.' }
    & dotnet tests/bin/Debug/net8.0/Tests.dll | Tee-Object -FilePath (Join-Path $repoPath 'artifacts/probe-tests.log')
    if ($LASTEXITCODE -ne 0) { throw 'Foundation probe failed.' }
    Write-Host 'Stage 2 narrows the historical probe to Core, storage and pure IPC framing. WPF/interop/hosts/process tests require the .NET 10 product build.'
}
finally { $env:CHROMATIC_TEST_ROOT = $previousTestRoot; Pop-Location }

