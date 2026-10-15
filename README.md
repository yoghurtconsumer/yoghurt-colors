# Chromatic Desktop

Independent Windows 10/11 display utility, **Stage 1 complete; Stage 2 color math in progress**. The production
target is C# WPF / .NET 10 / native x64 / Windows build 19041+. This checkpoint
does not apply display effects. Every unbuilt native provider reports
`NotImplemented`; no Agent listener, phone listener, hooks, registry writes or
hardware mutations are enabled.

The normative contract and sequenced work are in
[the specification](docs/TECHNICAL-SPECIFICATION.md) and
[build prompts](docs/CODEX-BUILD-PROMPTS.md). Read
[implementation state](docs/IMPLEMENTATION-STATE.md) before continuing.

## Run the app

Double-click **Run-Chromatic.cmd**, or run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/Run-App.ps1
```

The launcher explicitly builds the x64 app, starts its executable and waits for
the main window's first rendered frame before reporting success. It works from
another working directory and with spaces in the repository path. Failure keeps
the command window open and writes details to
`%LOCALAPPDATA%\ChromaticDesktop\logs\latest-launch.log`; build output and
per-process startup reports are in that same folder. No administrator rights are
needed. The Display page now includes local color-sample previews and Reset.

To verify the actual batch launcher and window lifecycle without keeping a
window open, run `./Run-Chromatic.cmd -SmokeTest`. Smoke mode uses in-memory
default settings and closes after the real window's first render.

Microsoft .NET SDK **10.0.402 x64** and the .NET/Desktop/ASP.NET Core **10.0.13**
runtimes are installed under `%LOCALAPPDATA%\ChromaticDesktop\dotnet` on this
machine. The scripts select that installation for their process; system PATH,
registry and the existing .NET 8 installation are unchanged by these scripts.
`global.json` pins the verified SDK exactly. The app targets
`net10.0-windows10.0.19041.0`, native x64.

## Build and test

```powershell
# Normal .NET 10 restore, Release build and all automated tests:
powershell -NoProfile -ExecutionPolicy Bypass -File eng/Test.ps1 -Locked

# Or select the SDK in this PowerShell session and use dotnet directly:
. ./eng/Use-DotNet.ps1
dotnet build ChromaticDesktop.sln -c Release -p:Platform=x64
dotnet test ChromaticDesktop.sln -c Release -p:Platform=x64 --no-build
```

Verified: all 13 projects build with **0 warnings / 0 errors**; **95 tests pass**
using actual .NET 10 and VSTest (46 Core, 44 integration, 5 UI/launcher). The native
x64 app also started, created its main window, and closed with exit code 0.
TRX results are written to `artifacts/test-results`. Hardware behavior remains
unimplemented and unqualified.

On another computer without the SDK, install it using the Microsoft archive
and SHA-512 verification script, then pin the installed SDK:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/Install-DotNet.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File eng/Pin-Sdk.ps1
```

NuGet restore downloads the Windows SDK targeting package and pinned test
packages as needed. Dependency lock files are included. The normal restore
retains online vulnerability auditing; the latest audit reported no known
vulnerable dependencies.

An optional historical source probe remains for a machine with only .NET 8;
it **does not qualify the .NET 10 build**:

```powershell
powershell -File eng/Test-Portable.ps1
```

The probe uses cached pinned xUnit packages and disables network vulnerability
auditing for that isolated run only. It is no longer the primary test path.
No test starts a hardware provider or changes display/registry/firewall settings.

## Boundaries

Core contains immutable intentions, typed commands, validation, reducer and
backend planning. Infrastructure contains versioned strict JSON and IPC contracts.
Agent/Recovery define the future writer/restore boundary and return explicit
failure at this checkpoint. App is a navigable WPF shell with themes, diagnostics,
Music panel and explicit exit versus notification-area behavior. Display sliders
edit a clearly labeled draft with local sample previews; Apply is unavailable
until the real Agent and independent recovery exist. Pure saturation/affine
matrices and baseline-composed tonal curves are implemented. Attributed CVD
correction, native binding/ownership and recovery remain incomplete Stage 2 work.

Settings are local and optional services default off. Magnification scope is the
whole desktop; gamma/warmth settings have explicit monitor identities. Holding
boost never changes the saved profile. Exiting requests restore and disposes
the tray; the disconnected client honestly reports that Agent restoration is not
implemented. There can be no Stage 1 owned display resources to restore.

See [traceability](docs/REQUIREMENTS-TRACEABILITY.md),
[capabilities](docs/CAPABILITIES.md) and [security](docs/SECURITY.md).
