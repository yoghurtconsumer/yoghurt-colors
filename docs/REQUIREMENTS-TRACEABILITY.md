# Requirements traceability — Stage 2

Status records implemented behavior, not hardware qualification. All 14 .NET 10
x64 projects build with SDK 10.0.402; 186 VSTest tests pass (58 Core, 113 integration,
15 UI/launcher). Native color routes now require authenticated ownership, verified
SDR, journal-before-write, readback and live independent recovery. All automated
resource tests inject fakes. No physical display qualification was run.

Private friend-test packaging is now available on the same target: optimized
self-contained App/Agent/Recovery with bundled .NET 10.0.13, three startup checks
with SDK/runtime discovery paths removed and complete ZIP-entry hash verification.
The follow-up full solution validation used Debug/x64 (186 tests), because a live
Release app held its files. This is not a public release or Stage 7 completion.

| ID | Requirement | Actual Stage 2 status | Evidence / boundary | Remaining stage / qualification |
| --- | --- | --- | --- | --- |
| F01 | Whole-screen saturation through 300%; 100% neutral | Guarded native matrix and atomic explicit Apply implemented; false SDR block fixed | Exact 300% math, MAG ABI, owning STA, real baseline/readback/restore; UI-to-Agent fake-process test; modern active-color-mode regression | Physical GPU/OS qualification |
| F02 | Native NVIDIA digital vibrance; software AMD/Intel equivalent; software beyond NVIDIA limit | Optional private adapter, draft preparation UI and safe software fallback implemented; experimental | Explicit opt-in, neutral saturation/separate vibrance, actual backend status; System32-only ABI, GDI mapping/ranges, route restore, fake no-double-vibrance/fallback tests | Fullscreen/private ABI/hybrid/driver hardware matrix |
| F03 | Brightness, contrast, temperature and gamma | Global affine matrix and optional selected-output gamma implemented | Journaled composed matrix/LUT; exact captured calibration; slow/ignored failures roll back | Single-output hardware qualification; shared-ramp gate remains |
| F04 | Highlights and shadows sliders in phone UI | Desktop sliders/curves and optional SDR LUT implemented; phone deferred | Monotonic bounded curves, preview, explicit gamma opt-in/output; no phone listener | Physical LUT qualification / 6 |
| F05 | Scene looks for treelines, interiors/dark rooms, night, snow glare, smoke/dust, range/bright sky | Deferred | PresetDocument contract; no built-in scene payloads | 3 |
| F06 | Saved custom looks | Foundation only | Atomic ProfileRepository/PresetRepository; no library CRUD UI | 3 |
| F07 | Automatic game-specific colors | Foundation only | GameRule, ExecutableIdentity, pure ProfileResolver and reducer | 3 |
| F08 | Preset hotkeys | Deferred | No global hotkey registration | 3 |
| F09 | Red-/green-/blue-weak color modes | Adjustable global approximation implemented | Attributed severity-1 coefficients; row orientation/correction tests; UI mode/strength; COLOR-PROVENANCE.md | Visual qualification; approximation remains explicit |
| F10 | Hold Dark Boost, selectable tint; release restores normal look; Mouse 5 demo | Curve/tint/reducer and injectable qualified gamma route implemented; live hold unavailable | Generation fences direct/real pipe; baseline composition; measured write duration; unqualified holds refused; basic affine lift math | 3 input + explicit latency/hardware qualification |
| F11 | Resolution and refresh switching | Foundation only | Rational DisplayIntent; PreviewTransaction states; Resolution shell | 4 |
| F12 | HDR on/off | Foundation only | HDR intent/capability; HdrBackend returns NotImplemented | 4 |
| F13 | Stretched 4:3 | Foundation only | ScalingIntent.Stretched; no mode/scaling write | 4 |
| F14 | Custom modes including square sizes; kept NVIDIA mode saved in driver list | Deferred | No custom mode provider or enumerated mode inventory | 4 |
| F15 | Crosshair shape, size, gap, color and outline | Foundation only | CrosshairSettings/ranges; Crosshair shell; OverlayHost NotImplemented | 5 |
| F16 | Picture crosshairs and X/Y offsets (2.1.12) | Foundation only | Asset hash/offset model; image decoder and placement deferred | 5 |
| F17 | Library of shared looks/crosshairs; public page also lists resolutions/full setups | Foundation only | Portable typed presets and Library shell; sharing/catalog absent | 3 / optional 7 |
| F18 | Short preset codes; website-to-app link; confirmation before application | Deferred | No share code, protocol registration, deep link or import confirmation | 3 |
| F19 | Catalog filter by type/game, newest/most-copied, author/date; publish/report; image moderation | Optional, deferred | No catalog/publish/report endpoints | optional 7 |
| F20 | Wi-Fi phone color sliders/scene presets via QR; server lives only while Phone window is open | Foundation only | Remote schema/color whitelist/sequence validation; PhoneServer always off | 6 |
| F21 | Music controls, improved app support (2.1.10) | Foundation only | Music panel and typed IMediaController; GSMTC NotImplemented | 6 |
| F22 | Per-monitor gamma/warmth | Explicit selected-output route implemented; multiple outputs blocked pending alias qualification | Display DC, stable path/runtime mapping checks, 1536-byte capture/restore, per-output warmth; shared-ramp limitation disclosed | Hardware/shared-ramp qualification / 4 |
| F23 | Monitor hardware brightness/color over display cable | Foundation only | IMonitorBackend allowlisted controls; DDC returns NotImplemented | 4 |
| F24 | Color watchdog following game/display changes | Process liveness, safety suspension/restoration and preservation of confirmed SDR routes on unchanged mappings implemented | Actual handler/fake tests: repeated resolution notifications do not disable matrix/native DVC or issue setters; unsafe mappings/tonal routes still restore; no automatic reapply | 4 bounded reapply/hotplug/fullscreen qualification |
| F25 | Default/Slate appearance; user picture/GIF background | Partial implementation | Default/Slate XAML and theme commands; local media deferred | 1 / 5 |
| F26 | Windows appearance customization: dark mode, accent, taskbar alignment/transparency, some Explorer options | Deferred | Customize shell; no registry or Windows appearance calls | 5 |
| F27 | Start with Windows | Deferred, default off | OptionalServices.Startup=false; no registration adapter | 5 |
| F28 | Recording-friendly color routing; detection notice and setup test | Foundation only | Planner capture-routing limitations; no capture qualification | 5 |
| F29 | Screenshot hotkey, local file and clipboard with color changes | Deferred | No capture/hotkey/clipboard/export implementation | 5 |
| F30 | Installed Steam game discovery | Deferred | Games shell; no Steam registry/manifests accessed | 3 |
| F31 | Local Discord activity including selected preset/game | Optional, default off | OptionalServices.Discord=false; no outgoing integration | 5 |
| F32 | Account sign-in using short-lived loopback browser callback; licence key entry/account page | Optional, deferred | No account required; no identity callback or endpoint | optional 7 |
| F33 | Plans, feature locks, separate Dark Boost entitlement, PC activation/release/move | Optional, deferred | Local utility has no feature locks; no commerce/activation service | optional 7 |
| F34 | Automatic signed update verification, release notes/What's New; status/revocation/check-in | Deferred, default off | OptionalServices.Updates=false; signing/updater absent | 7 |
| F35 | Local crash logs with build/OS; bug-report form with user-triggered technical report | Startup diagnostics, read-only metadata report and bounded companion logs implemented; full reporting deferred | Full SDK/build stderr, first-render logs, per-output raw flags/active mode, Agent apply/recovery logs; export/upload absent | 5 / 7 |
| F36 | Legal/licences page, uninstall retains user data, explicit cleanup | In-app page/uninstall deferred; private ZIP includes terms/notices | Original project licence status retained; runtime/SDK/generated-binding notices included; no installer or cleanup writes | 5 / public release 7 |
| F37 | Repair action can relaunch elevated | Deferred | asInvoker manifest; no elevation/repair path | 5 / 7 |
| F38 | Update rating controls documented but inactive | Inactive by reference | No update-rating controls or telemetry implemented/enabled | remains inactive |

Cross-cutting evidence: `tests/Chromatic.Core.Tests` exercises reducer/planner/ranges;
`tests/Chromatic.Integration.Tests` exercises real temporary-file atomic writes, faults,
schema/migration/IPC and injected backends; `tests/Chromatic.Ui.Tests` checks DI,
navigation, XAML and theme resource loading without showing windows. Exact test
results and qualification limits are recorded in IMPLEMENTATION-STATE.md.

The complete Section 2 directory tree is the final multi-stage target. All nine source
projects, three test projects and two separate hardware/process harnesses exist now.
Native color/math and active authenticated process hosts are implemented. Later
views/dialogs/assets, packaging/CI and optional-cloud service trees remain scoped
to later stages; empty files are not used to imply implementation. The manual
hardware harness was compiled but never executed.
