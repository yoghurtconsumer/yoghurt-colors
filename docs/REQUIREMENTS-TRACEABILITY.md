# Requirements traceability — Stage 1

Status records implemented behavior, not hardware qualification. All 13 .NET 10
x64 projects build with SDK 10.0.402; all 95 VSTest tests pass. The real app starts
and closes cleanly on Windows build 26200. Native APIs always return
NotImplemented. Foundation does not mean native features are operational.

| ID | Requirement | Actual Stage 1 status | Evidence / boundary | Remaining stage |
| --- | --- | --- | --- | --- |
| F01 | Whole-screen saturation through 300%; 100% neutral | Pure math/local preview implemented; desktop apply unavailable | Matrix5x5; exact 300% matrix and gray-invariance tests; Display sample preview | 2 |
| F02 | Native NVIDIA digital vibrance; software AMD/Intel equivalent; software beyond NVIDIA limit | Software math/routing implemented; native unavailable | Software vibrance cap; planner fallback and advanced-combination tests; NVIDIA stub | 2 |
| F03 | Brightness, contrast, temperature and gamma | Pure math and affine sample preview implemented | Composed affine controls; ToneCurveBuilder; GammaRampComposer; no physical writes | 2 |
| F04 | Highlights and shadows sliders in phone UI | Pure tone curves implemented; phone deferred | Bounded shadow/highlight math and monotonicity tests; no phone listener | 2 / 6 |
| F05 | Scene looks for treelines, interiors/dark rooms, night, snow glare, smoke/dust, range/bright sky | Deferred | PresetDocument contract; no built-in scene payloads | 3 |
| F06 | Saved custom looks | Foundation only | Atomic ProfileRepository/PresetRepository; no library CRUD UI | 3 |
| F07 | Automatic game-specific colors | Foundation only | GameRule, ExecutableIdentity, pure ProfileResolver and reducer | 3 |
| F08 | Preset hotkeys | Deferred | No global hotkey registration | 3 |
| F09 | Red-/green-/blue-weak color modes | Foundation only | Bounded CvdMode enum; no daltonization math/native apply | 2 |
| F10 | Hold Dark Boost, selectable tint; release restores normal look; Mouse 5 demo | Reducer and pure tone/tint composition implemented | Temporary hold; highlight preservation; exact baseline release tests; no hooks/native writes | 2–3 |
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
| F22 | Per-monitor gamma/warmth | Pure captured-baseline composition implemented; native unavailable | GammaRampComposer; 1536-byte round trip; calibration monotonicity; explicit per-monitor scope | 2 / 4 |
| F23 | Monitor hardware brightness/color over display cable | Foundation only | IMonitorBackend allowlisted controls; DDC returns NotImplemented | 4 |
| F24 | Color watchdog following game/display changes | Foundation only | Revision/generation fences; watchdog not implemented | 2 / 4 |
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
| F35 | Local crash logs with build/OS; bug-report form with user-triggered technical report | Startup diagnostics implemented; full reporting deferred | Local launcher/build/first-render/failure logs; rotation/export/report not implemented | 5 / 7 |
| F36 | Legal/licences page, uninstall retains user data, explicit cleanup | Deferred | No data deletion/uninstall; licensing selection remains a release gate | 5 / 7 |
| F37 | Repair action can relaunch elevated | Deferred | asInvoker manifest; no elevation/repair path | 5 / 7 |
| F38 | Update rating controls documented but inactive | Inactive by reference | No update-rating controls or telemetry implemented/enabled | remains inactive |

Cross-cutting evidence: `tests/Chromatic.Core.Tests` exercises reducer/planner/ranges;
`tests/Chromatic.Integration.Tests` exercises real temporary-file atomic writes, faults,
schema/migration/IPC and injected backends; `tests/Chromatic.Ui.Tests` checks DI,
navigation, XAML and theme resource loading without showing windows. Exact test
results and qualification limits are recorded in IMPLEMENTATION-STATE.md.

The complete Section 2 directory tree is the final multi-stage target. All nine source
projects and four test/harness projects exist now. Math/native binding implementation,
active process hosts, later views/dialogs/assets, packaging/CI and optional-cloud
service trees remain scoped to their later stages; empty files are not used to imply
implementation.
