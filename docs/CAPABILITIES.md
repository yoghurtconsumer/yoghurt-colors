# Stage 1 capabilities

All native capability entries are `Unsupported` with `NotImplemented: Stage 1
foundation`. Available is never inferred from an OS version or a successful
desired-state edit. `ApplyResult.Success` is reserved for completed operations;
unit tests explicitly mark in-memory fake success as such.

| Component | Stage 1 behavior | Activation gate |
| --- | --- | --- |
| Global Magnification | NotImplemented | Stage 2: STA, baseline, ownership, journal, recovery, readback |
| NVIDIA DVC | NotImplemented | Stage 2: output mapping and qualified private ABI; opt-in |
| SDR gamma/warmth | NotImplemented | Stage 2: exact per-output baseline; opt-in; refuse HDR/ACM |
| Display modes / HDR / DDC | NotImplemented | Stage 4: inventory, capability checks, guarded transactions |
| Crosshair | Off, NotImplemented | Stage 5: external window and validated assets |
| Phone server | Off, NotImplemented | Stage 6: authenticated Private-LAN lifecycle |
| GSMTC | NotImplemented | Stage 6: real session discovery/transport |
| WPF navigation / themes | Implemented; real .NET 10 build/tests and process smoke passed | Interactive tray/accessibility and OS matrix remain manual |
| Versioned local JSON | Implemented | Strict schema + semantic validation; no implicit corrupt reset |

The planner never substitutes independent gamma ramps for saturation and never
claims per-monitor Magnification. HDR/advanced color blocks SDR operations.
Native vibrance is a separate normalized intention and requires output mapping;
no implicit double saturation/DVC pass. Capture routing omits native gamma and
does not promise capture parity. Missing nonlinear processing is a limitation,
not silent success.
