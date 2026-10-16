param([Parameter(Mandatory = $true)][string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackageDirectory).Path
$reports = @()
function Assert-BundledRuntime($Report, [string]$Directory) {
    $prefix = [IO.Path]::GetFullPath($Directory).TrimEnd('\') + '\'
    if ($Report.architecture -ne 'X64' -or $Report.framework -notlike '.NET 10.*' -or
        -not ([IO.Path]::GetFullPath($Report.coreLibrary)).StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Executable did not use its own .NET 10 x64 runtime: $Directory"
    }
}
foreach ($role in @('App', 'Agent', 'Recovery')) {
    $directory = if ($role -eq 'App') { $package } else { Join-Path $package "companions/$role" }
    $image = Join-Path $directory "Chromatic.$role.exe"
    foreach ($file in @("Chromatic.$role.exe", "Chromatic.$role.dll", "Chromatic.$role.runtimeconfig.json",
        'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'System.Private.CoreLib.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $directory $file))) { throw "Missing package dependency: $role/$file" }
    }
    $config = Get-Content -LiteralPath (Join-Path $directory "Chromatic.$role.runtimeconfig.json") -Raw | ConvertFrom-Json
    if ($config.runtimeOptions.framework -or $config.runtimeOptions.frameworks -or -not $config.runtimeOptions.includedFrameworks) {
        throw "$role is framework-dependent rather than self-contained."
    }
    $launchId = [Guid]::NewGuid().ToString('N')
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $image
    $start.Arguments = '--startup-smoke-test'
    $start.WorkingDirectory = $env:TEMP
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    # Prove the published apphost uses the runtime alongside its executable,
    # rather than relying on this developer machine's SDK/runtime environment.
    $start.EnvironmentVariables['PATH'] = "$env:SystemRoot\System32;$env:SystemRoot"
    $start.EnvironmentVariables['DOTNET_ROOT'] = Join-Path $package 'nonexistent-dotnet'
    $start.EnvironmentVariables['DOTNET_ROOT_X64'] = Join-Path $package 'nonexistent-dotnet'
    $start.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $start.EnvironmentVariables['CHROMATIC_LAUNCH_ID'] = $launchId
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw "Could not start packaged $role" }
        $output = $process.StandardOutput.ReadToEndAsync()
        $errorOutput = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(45000)) {
            $process.Kill(); $process.WaitForExit()
            throw "Packaged $role startup smoke timed out; only this no-hardware smoke child was stopped."
        }
        if ($process.ExitCode -ne 0) { throw "Packaged $role failed: $($errorOutput.GetAwaiter().GetResult())" }
        if ($role -eq 'App') {
            $reportPath = Join-Path $env:LOCALAPPDATA "ChromaticDesktop/logs/startup-$launchId.json"
            $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
            if ($report.status -ne 'Ready' -or -not $report.windowVisible -or -not $report.smokeTest -or $report.processId -ne $process.Id) {
                throw 'Packaged WPF window did not acknowledge a clean, visible smoke render.'
            }
        }
        else {
            $report = $output.GetAwaiter().GetResult() | ConvertFrom-Json
            if ($report.role -ne $role -or -not $report.supported -or $report.hardwareWritesPerformed) { throw "Invalid $role smoke report." }
        }
        Assert-BundledRuntime $report $directory
        $reports += [PSCustomObject]@{ role = $role; framework = $report.framework; architecture = $report.architecture;
            bundledRuntimeVerified = $true; coreLibraryFile = 'System.Private.CoreLib.dll'; hardwareWritesPerformed = $false }
        Write-Host "Packaged $role passed: bundled .NET 10 x64; no hardware operations."
    }
    finally { $process.Dispose() }
}
$reports | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $package 'startup-verification.json') -Encoding UTF8
