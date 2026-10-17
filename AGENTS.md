# Chromatic Desktop

The normative contract is docs/TECHNICAL-SPECIFICATION.md. Read the current
docs/IMPLEMENTATION-STATE.md before changing scope. Update that file and
docs/REQUIREMENTS-TRACEABILITY.md after each stage. Stop at the requested stage.

Production: C# WPF, .NET 10, net10.0-windows10.0.19041.0, win-x64. Never downgrade
the product to make a local SDK pass. Runtime-gate APIs introduced after 19041.
Run `dotnet build ChromaticDesktop.sln`, then `dotnet test ChromaticDesktop.sln`.
The user-local .NET 10 SDK is selected by `. ./eng/Use-DotNet.ps1`; scripts
eng/Test.ps1 and eng/Run-App.ps1 select it automatically. Use eng/Install-DotNet.ps1
on a machine missing the SDK; it verifies the official Microsoft archive hash.
If SDK 10 is absent, use `powershell -File eng/Test-Portable.ps1` for the explicitly
isolated .NET 8 source probe; it does not qualify WPF, interop or the product.
At Stage 2 that historical probe covers only Core, persistence and pure framing.
Automated child-process recovery tests use the separate Chromatic.ProcessHarness
with injected file adapters. Never run Chromatic.HardwareHarness automatically.

Never read/write game VM, inject DLLs, hook DirectX, modify game files, install
drivers, or promise anti-cheat approval. Ordinary development/tests must never
alter physical displays, registry, startup, firewall or LAN exposure. Tests use
injected fake adapters only. Native implementations at Stage 1 return
NotImplemented, including restore; never report a successful hardware mutation.

Core is pure: no HWND, P/Invoke, filesystem, registry or network. Infrastructure
owns persistence/IPC. Agent alone will own hardware and input. Recovery must wait
for verified Agent termination before ownership. App hosts Media/Overlay/Remote.
No raw native calls through public IPC. Journal the real baseline before writes;
fence queued writes by revision AND ownership generation. Holds are temporary;
save only base profiles. Magnification is global, gamma/DDC are per monitor.

No automatic hardware tests. No Stage 2 native APIs in Stage 1. Do not add empty
future files or claim the final Section 2 tree is already implemented. Missing
later-stage paths and release gates belong in the durable progress notes.

Stage 3 listeners are explicit opt-ins owned by Agent. Automated tests inject
IAutomationInput and never install hooks. Keep low-level hooks out-of-process
from games, always chain, never synthesize/suppress/log text. Foreground uses
limited query metadata only. Protocol registration is a user-clicked HKCU action;
tests must inject IProtocolRegistry. Link handoff authenticates OS peers and must
never bypass import confirmation or Apply. Stop before Stage 4 unless requested.

Stage 4 step 1 now has read-only, paged Agent display inventory and local draft
plans. Inject IDisplayInventoryApi / readDisplays for tests; never probe hardware
in ordinary tests or the process harness. Queries must not advance revisions or
change observed colours/hold. Preserve snapshot IDs and invalidate on broadcasts.
Stage 4 step 2 adds explicit opt-in SDR source-size/scaling previews at captured
physical timing. Inject ICcdModeApi / IDisplayModeResource: never run native mode
setters in ordinary tests. display-preview.json is separate from recovery.json;
Recovery verifies death and writer lease before topology then colour restoration.
Agent owns the monotonic 15-second deadline. Keep releases temporary topology
ownership only after durable confirmation. No Windows database save, auto game
mode switches, HDR toggle, physical refresh changes, CDS or DDC implementation
is claimed. Native SDK ABI and physical GPU qualification remain open. Stop
before Stage 5; complete remaining Stage 4 work only when requested.
