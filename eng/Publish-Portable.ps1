param([string]$RuntimeVersion = '10.0.13')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Use-DotNet.ps1')
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$distribution = Join-Path $repository "artifacts/distribution/$buildId"
$package = Join-Path $distribution 'ChromaticDesktop'
$archive = Join-Path $distribution 'ChromaticDesktop-Windows-x64.zip'
New-Item -ItemType Directory -Path $package -Force | Out-Null
Push-Location $repository
try {
    $sdk = (& dotnet --version).Trim()
    $expectedSdk = (Get-Content global.json -Raw | ConvertFrom-Json).sdk.version
    if ($LASTEXITCODE -ne 0 -or $sdk -ne $expectedSdk) { throw "Required .NET SDK $expectedSdk is unavailable. Use eng/Install-DotNet.ps1." }
    foreach ($role in @('App', 'Agent', 'Recovery')) {
        $destination = if ($role -eq 'App') { $package } else { Join-Path $package "companions/$role" }
        # A separate optimized configuration avoids replacing binaries in a
        # running developer app. All target/framework/architecture settings stay.
        & dotnet publish "src/Chromatic.$role/Chromatic.$role.csproj" -c PortableTest -r win-x64 --self-contained true -o $destination `
            -p:Platform=x64 -p:Optimize=true -p:DebugType=None -p:DebugSymbols=false `
            -p:PublishSingleFile=false -p:PublishTrimmed=false -p:PublishReadyToRun=false `
            -p:RestoreLockedMode=true
        if ($LASTEXITCODE -ne 0) { throw "Self-contained publish failed for $role. Partial artifacts preserved at $distribution" }
        $runtimeConfig = Get-Content -LiteralPath (Join-Path $destination "Chromatic.$role.runtimeconfig.json") -Raw | ConvertFrom-Json
        if (@($runtimeConfig.runtimeOptions.includedFrameworks | Where-Object { $_.version -ne $RuntimeVersion }).Count -gt 0) {
            throw "SDK-selected runtime differs from $RuntimeVersion; review and update package runtime/notices together."
        }
    }
    $notices = Join-Path $package 'notices/project'
    New-Item -ItemType Directory -Path $notices -Force | Out-Null
    foreach ($file in @('LICENSE', 'THIRD-PARTY-NOTICES.md', 'docs/THIRD-PARTY-DEPENDENCIES.md', 'docs/COLOR-PROVENANCE.md', 'docs/ANTI-CHEAT-BOUNDARY.md')) {
        Copy-Item -LiteralPath $file -Destination $notices
    }
    Copy-Item -LiteralPath 'docs/FRIEND-TEST-README.txt' -Destination (Join-Path $package 'READ-ME-FIRST.txt')
    Copy-Item -LiteralPath 'docs/notices/CsWin32-LICENSE.txt' -Destination $notices
    $cacheLine = & dotnet nuget locals global-packages --list
    if ($LASTEXITCODE -ne 0) { throw 'Could not locate dependency notices.' }
    $cache = ($cacheLine -join '').Substring(($cacheLine -join '').IndexOf(':') + 1).Trim()
    $components = @(
        @('microsoft.netcore.app.runtime.win-x64', $RuntimeVersion),
        @('microsoft.windowsdesktop.app.runtime.win-x64', $RuntimeVersion),
        @('microsoft.aspnetcore.app.runtime.win-x64', $RuntimeVersion),
        @('microsoft.windows.cswin32', '0.3.363'),
        @('microsoft.windows.sdk.win32metadata', '71.0.30-preview')
    )
    foreach ($component in $components) {
        $source = Join-Path $cache ($component[0] + '/' + $component[1])
        $terms = @(Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Name -match '(?i)license|notice|third.party' })
        if ($terms.Count -eq 0) { throw "Missing upstream licence/notice files for $($component[0])." }
        $target = Join-Path $package ('notices/' + $component[0])
        New-Item -ItemType Directory -Path $target -Force | Out-Null
        foreach ($term in $terms) { Copy-Item -LiteralPath $term.FullName -Destination $target }
    }
    if (Get-ChildItem -LiteralPath $package -Recurse -File | Where-Object { $_.Name -match '(?i)\.Tests\.|xunit|testhost|nvapi64\.dll|\.pdb$|^settings\.json$|^recovery\.json|\.log$' }) {
        throw 'A test/development binary or NVIDIA driver DLL was accidentally included.'
    }
    $sourceCommit = (& git rev-parse HEAD).Trim()
    $dirty = [bool](& git status --porcelain)
    [ordered]@{ product = 'Chromatic Desktop'; audience = 'Private invited tester'; publicRelease = $false;
        sourceCommit = $sourceCommit; workingTreeDirty = $dirty; createdUtc = [DateTimeOffset]::UtcNow.ToString('O');
        sdk = $sdk; runtime = $RuntimeVersion; framework = 'net10.0-windows10.0.19041.0'; rid = 'win-x64';
        configuration = 'PortableTest'; optimized = $true; selfContained = $true; signedRelease = $false;
        requiresSdk = $false; requiresSeparateDotnet = $false; hardwareQualificationPerformed = $false } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'build-info.json') -Encoding UTF8
    & (Join-Path $PSScriptRoot 'Test-PortablePackage.ps1') -PackageDirectory $package
    $hashes = foreach ($file in Get-ChildItem -LiteralPath $package -Recurse -File | Sort-Object FullName) {
        $relative = $file.FullName.Substring($package.Length + 1).Replace('\', '/')
        (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $relative
    }
    $hashes | Set-Content -LiteralPath (Join-Path $package 'files.sha256') -Encoding UTF8
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($package, $archive, [IO.Compression.CompressionLevel]::Optimal, $true)
    $expectedHashes = @{}
    foreach ($line in $hashes) { $expectedHashes['ChromaticDesktop/' + $line.Substring(66)] = $line.Substring(0, 64) }
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName.EndsWith('/') -or $entry.FullName -eq 'ChromaticDesktop/files.sha256') { continue }
            if (-not $expectedHashes.ContainsKey($entry.FullName)) { throw "Unexpected ZIP entry: $($entry.FullName)" }
            $stream = $entry.Open(); $sha = [Security.Cryptography.SHA256]::Create()
            try { $actualHash = ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
            finally { $sha.Dispose(); $stream.Dispose() }
            if ($actualHash -ne $expectedHashes[$entry.FullName]) { throw "ZIP file verification failed: $($entry.FullName)" }
            $expectedHashes.Remove($entry.FullName)
        }
        if ($expectedHashes.Count -ne 0) { throw 'ZIP is missing expected package files.' }
    }
    finally { $zip.Dispose() }
    $archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    "$archiveHash  $([IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath ($archive + '.sha256') -Encoding ASCII
    Write-Host "Private test ZIP: $archive"
    Write-Host "Size: $([Math]::Round((Get-Item -LiteralPath $archive).Length / 1MB, 1)) MiB"
    $archive | Set-Content -LiteralPath (Join-Path $repository 'artifacts/latest-private-test-package.txt') -Encoding UTF8
}
finally { Pop-Location }
