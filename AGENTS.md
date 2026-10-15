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
