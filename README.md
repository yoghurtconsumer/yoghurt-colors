# Chromatic Desktop

Independent Windows 10/11 display utility. Stage 2 implements guarded color
engines, authenticated Agent IPC and independent recovery. Production is C# WPF,
.NET 10, native x64, Windows build 19041+. This is a development build: physical
GPU/monitor behavior remains unqualified and later-stage features are unavailable.

The normative contract is [the specification](docs/TECHNICAL-SPECIFICATION.md).
Read [implementation state](docs/IMPLEMENTATION-STATE.md) and
[F01–F38 traceability](docs/REQUIREMENTS-TRACEABILITY.md) before continuing.

## Run and try it

Double-click **Run-Chromatic.cmd**. It builds the app and companions, shows the
main window and records first render. Errors keep the console open and point to
%LOCALAPPDATA%/ChromaticDesktop/logs/latest-launch.log.

Sliders initially affect sample previews only. Effects always start off.
On a verified SDR desktop with connected Agent/Recovery, **Apply colors to desktop**
requests the global matrix and reports actual completion/readback or failure.
**Restore** and exiting request the captured baseline. Magnification affects
every monitor; selecting an output scopes only independent LUT/DVC intentions.

Gamma compatibility and private NVIDIA DVC require separate Settings opt-ins,
Save and restart. Gamma/shadows/highlights need an explicit output. Multiple active
gamma outputs are blocked pending shared-ramp qualification; ignored or slow LUTs
disable the session's route. HDR/ACM/unknown mappings refuse SDR grading.
Native DVC remains experimental and can fall back to software after safe restore.
CVD correction and software vibrance are explicitly approximate.

On an unfinished journal, the app offers local baseline recovery before starting
a new writer. Foreign/conflicting values and corrupt journals are preserved for
review. Session/companion logs are at
%LOCALAPPDATA%/ChromaticDesktop/sessions/<sessionId>/.

A safe launcher check is **Run-Chromatic.cmd -SmokeTest**: in-memory defaults,
no companions/providers, first rendered frame, automatic close. No administrator
rights or permanent PATH change are needed.

## Build and automated tests

Microsoft SDK **10.0.402 x64** and .NET/Desktop/ASP.NET Core **10.0.13** are
installed in %LOCALAPPDATA%/ChromaticDesktop/dotnet. The helpers select that
installation for their process. global.json pins it exactly; the Windows target
is net10.0-windows10.0.19041.0 / win-x64.

    powershell -NoProfile -ExecutionPolicy Bypass -File eng/Test.ps1 -Locked

Or in PowerShell:

    . ./eng/Use-DotNet.ps1
    dotnet build ChromaticDesktop.sln -c Release -p:Platform=x64
    dotnet test ChromaticDesktop.sln -c Release -p:Platform=x64 --no-build

Verified: **14 projects, zero build warnings/errors, 140 passing VSTest tests**
(52 Core, 81 integration, 7 UI/launcher). TRX results are under artifacts/test-results.
Tests cover math/ABI, strict schemas/migrations, atomic write failures, owned
baselines, stale revisions, real OS-authenticated pipes and five independent
child-process recovery cases. Those processes use file-backed fake resources.
No automated test mutates a physical display, registry, firewall or startup state.

The manual hardware harness is built but **was not run**. Its explicit flags,
interactive acknowledgment, countdown and independent rollback are documented in
[manual qualification](docs/MANUAL-TESTS.md). Never include it in ordinary CI.

On another machine without the SDK:

    powershell -NoProfile -ExecutionPolicy Bypass -File eng/Install-DotNet.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File eng/Pin-Sdk.ps1

The installer verifies Microsoft's archive SHA-512. System .NET 8 remains untouched.
The optional historical eng/Test-Portable.ps1 probe covers only isolated .NET 8
Core/storage/framing source; it does not build or qualify this product.

## Boundaries

Core is pure. Agent owns normal display writes on its STA. Recovery verifies Agent
termination and acquires the exclusive writer lease before restoring. Baselines
and pending writes are atomically journaled before setters; no foreign value is
silently overwritten. No game VM access, injection, DirectX hooks, game-file edits,
drivers or anti-cheat guarantees.

Input/game profiles and library/imports are Stage 3; full display/HDR/DDC
transactions Stage 4; overlay/capture Stage 5; phone/media Stage 6. Those
providers remain honest NotImplemented. No phone/LAN listener, registry startup,
cloud, Discord or updater service is enabled. Packaging/signing/licensing are
Stage 7. See [capabilities](docs/CAPABILITIES.md),
[compatibility evidence](docs/COMPATIBILITY-MATRIX.md),
[security](docs/SECURITY.md) and [dependency provenance](docs/THIRD-PARTY-DEPENDENCIES.md).
