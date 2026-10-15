param([switch]$SkipShell)
$ErrorActionPreference = 'Stop'
$repoPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$probePath = Join-Path $repoPath '.stage1-probe'
New-Item -ItemType Directory -Force $probePath | Out-Null
$sdkLines = & dotnet --list-sdks
$sdkVersion = ($sdkLines | Where-Object { $_ -match '^8\.0\.' } | Select-Object -Last 1).Split(' ')[0]
if (-not $sdkVersion) { throw 'The optional source probe requires an installed .NET 8 SDK. Production still requires .NET 10.' }
@{ sdk = @{ version = $sdkVersion; rollForward = 'disable'; allowPrerelease = $false } } | ConvertTo-Json | Set-Content (Join-Path $probePath 'global.json')
'<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><LangVersion>12</LangVersion><TreatWarningsAsErrors>true</TreatWarningsAsErrors><EnableDefaultCompileItems>false</EnableDefaultCompileItems><PlatformTarget>x64</PlatformTarget><RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources></PropertyGroup></Project>' | Set-Content (Join-Path $probePath 'Directory.Build.props')
foreach ($subdirectory in @('foundation', 'tests', 'shell', 'ui')) { New-Item -ItemType Directory -Force (Join-Path $probePath $subdirectory) | Out-Null }
@'
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <Compile Include="../../src/**/*.cs" Exclude="../../src/**/obj/**;../../src/**/bin/**;../../src/Chromatic.App/**;../../src/**/Program.cs" />
    <EmbeddedResource Include="../../docs/schemas/*.schema.json" LogicalName="Chromatic.Schemas.%(Filename)%(Extension)" />
  </ItemGroup>
</Project>
'@ | Set-Content (Join-Path $probePath 'foundation/Foundation.csproj')
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><IsPackable>false</IsPackable></PropertyGroup>
  <ItemGroup><ProjectReference Include="../foundation/Foundation.csproj" /></ItemGroup>
  <ItemGroup><Compile Include="../../tests/Chromatic.Core.Tests/*.cs" /><Compile Include="../../tests/Chromatic.Integration.Tests/*.cs" /><Compile Include="../../eng/SourceProbeRunner.cs" /></ItemGroup>
  <ItemGroup><PackageReference Include="xunit" /></ItemGroup>
</Project>
'@ | Set-Content (Join-Path $probePath 'tests/Tests.csproj')
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net8.0-windows</TargetFramework><UseWPF>true</UseWPF><UseWindowsForms>true</UseWindowsForms><ImplicitUsings>disable</ImplicitUsings><OutputType>WinExe</OutputType><AssemblyName>Chromatic.App</AssemblyName><ApplicationManifest>../../src/Chromatic.App/app.manifest</ApplicationManifest></PropertyGroup>
  <ItemGroup><ProjectReference Include="../foundation/Foundation.csproj" /><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>
  <ItemGroup><Compile Include="../../src/Chromatic.App/**/*.cs" Exclude="../../src/Chromatic.App/**/obj/**;../../src/Chromatic.App/**/bin/**" /><ApplicationDefinition Include="../../src/Chromatic.App/App.xaml" Link="App.xaml" /><Page Include="../../src/Chromatic.App/**/*.xaml" Exclude="../../src/Chromatic.App/App.xaml;../../src/Chromatic.App/**/obj/**;../../src/Chromatic.App/**/bin/**" Link="%(RecursiveDir)%(Filename)%(Extension)" /></ItemGroup>
</Project>
'@ | Set-Content (Join-Path $probePath 'shell/Shell.csproj')
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net8.0-windows</TargetFramework><UseWPF>true</UseWPF><OutputType>Exe</OutputType><IsPackable>false</IsPackable></PropertyGroup>
  <ItemGroup><ProjectReference Include="../shell/Shell.csproj" /><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>
  <ItemGroup><Compile Include="../../tests/Chromatic.Ui.Tests/*.cs" Exclude="../../tests/Chromatic.Ui.Tests/LauncherProcessTests.cs" /><Compile Include="../../eng/SourceProbeRunner.cs" /></ItemGroup>
  <ItemGroup><PackageReference Include="xunit" /></ItemGroup>
</Project>
'@ | Set-Content (Join-Path $probePath 'ui/Ui.csproj')
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
    if (-not $SkipShell) {
        & dotnet build ui/Ui.csproj -p:NuGetAudit=false -p:BuildInParallel=false -p:UseSharedCompilation=false -m:1 -nr:false
        if ($LASTEXITCODE -ne 0) { throw 'Shell probe build failed.' }
        & dotnet ui/bin/Debug/net8.0-windows/Ui.dll | Tee-Object -FilePath (Join-Path $repoPath 'artifacts/probe-ui.log')
        if ($LASTEXITCODE -ne 0) { throw 'Shell probe failed.' }
    }
}
finally { $env:CHROMATIC_TEST_ROOT = $previousTestRoot; Pop-Location }

