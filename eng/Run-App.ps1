param([switch]$NoBuild, [switch]$SmokeTest)
$ErrorActionPreference = 'Stop'
$launchExitCode = 1
$appProcess = $null
$launchId = [Guid]::NewGuid().ToString('N')
$logDirectory = Join-Path $env:LOCALAPPDATA 'ChromaticDesktop/logs'
$buildLog = Join-Path $logDirectory "build-$launchId.log"
$startupReport = Join-Path $logDirectory "startup-$launchId.json"
$launchLog = Join-Path $logDirectory 'latest-launch.log'
$previousLaunchId = $env:CHROMATIC_LAUNCH_ID
Push-Location (Join-Path $PSScriptRoot '..')
try {
    New-Item -ItemType Directory -Force $logDirectory | Out-Null
    "$(Get-Date -Format o) Starting Chromatic Desktop from $((Get-Location).Path)" | Set-Content -LiteralPath $launchLog
    . (Join-Path $PSScriptRoot 'Use-DotNet.ps1')
    $dotnetExecutable = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    "dotnet: $dotnetExecutable" | Add-Content -LiteralPath $launchLog
    if (-not $NoBuild) {
        Write-Host 'Building Chromatic Desktop (.NET 10 x64)...'
        & $dotnetExecutable build 'src/Chromatic.App/Chromatic.App.csproj' --configuration Release -p:Platform=x64 --nologo *> $buildLog
        if ($LASTEXITCODE -ne 0) {
            Get-Content -LiteralPath $buildLog -Tail 25 | ForEach-Object { Write-Host $_ }
            throw "Build failed. Details: $buildLog"
        }
    }
    $appPath = Join-Path (Get-Location) 'src/Chromatic.App/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/Chromatic.App.exe'
    if (-not (Test-Path -LiteralPath $appPath)) { throw 'The app executable is missing. Run this launcher without -NoBuild.' }
    $env:CHROMATIC_LAUNCH_ID = $launchId
    $startArguments = @{ FilePath = $appPath; WorkingDirectory = (Split-Path $appPath); PassThru = $true; WindowStyle = 'Normal' }
    if ($SmokeTest) { $startArguments.ArgumentList = '--startup-smoke-test' }
    $appProcess = Start-Process @startArguments
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    $report = $null
    do {
        if (Test-Path -LiteralPath $startupReport) {
            $report = Get-Content -LiteralPath $startupReport -Raw | ConvertFrom-Json
            if ($report.processId -ne $appProcess.Id) { throw 'Startup acknowledgment came from an unexpected process.' }
            if ($report.status -eq 'Failed') { throw $report.message }
            if ($report.status -eq 'Ready' -and $report.windowVisible) { break }
        }
        if ($appProcess.HasExited) { throw "The app exited before showing its window (exit $($appProcess.ExitCode))." }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($null -eq $report -or $report.status -ne 'Ready' -or -not $report.windowVisible) {
        throw "The app did not report a visible window within 30 seconds. Startup report: $startupReport"
    }
    Write-Host "Chromatic Desktop is open (PID $($appProcess.Id))."
    "Ready: process $($appProcess.Id), visible window acknowledged. Report: $startupReport" | Add-Content -LiteralPath $launchLog
    if ($SmokeTest) {
        if (-not $appProcess.WaitForExit(15000)) { throw 'The startup smoke test did not close cleanly.' }
        if ($appProcess.ExitCode -ne 0) { throw "Startup smoke test exited with code $($appProcess.ExitCode)." }
        Write-Host 'Startup smoke test passed: rendered a visible window and exited cleanly.'
    }
    $launchExitCode = 0
}
catch {
    $failure = "Chromatic Desktop could not start: $($_.Exception.Message)"
    Write-Host $failure -ForegroundColor Red
    Write-Host "Launch log: $launchLog"
    try { $failure | Add-Content -LiteralPath $launchLog } catch { }
    # Clean up only the child started by this invocation after a failed launch.
    if ($null -ne $appProcess -and -not $appProcess.HasExited) { $appProcess.Kill(); $appProcess.WaitForExit() }
}
finally {
    $env:CHROMATIC_LAUNCH_ID = $previousLaunchId
    if ($null -ne $appProcess) { $appProcess.Dispose() }
    Pop-Location
}
exit $launchExitCode
