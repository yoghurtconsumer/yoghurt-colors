# Chromatic Desktop — seven build prompts

 Seven sequenced, self-contained Codex prompts

Save this specification at `docs/TECHNICAL-SPECIFICATION.md` in the new repository **before Prompt 1**. Each prompt below can run in a new chat against that repository. The file, schemas, tests and `IMPLEMENTATION-STATE.md` carry context across chats; the agent must not depend on previous chat history. These are seven implementation stages, not seven messages asking the agent to output an entire repository as text. Let it write and test real files. Each prompt intentionally scopes work so that the result is reviewable and compiles before the next stage.

Use the prompts in order. When a stage has unfinished work, resume that stage from its saved checkpoint before moving to the next. Context loss must not silently convert missing implementation into success. All stages forbid changing the current physical desktop during ordinary development; native mutation is exercised only through the explicit hardware harness.

### Prompt 1 of 7 — Foundation, contracts, state and persistence

```text
Build stage 1 of Chromatic Desktop, an independently branded Windows 10/11
display utility. Work in this repository and write code; do not just propose it.
Read docs/TECHNICAL-SPECIFICATION.md Sections 1, 2 and 3.1, then inspect any
AGENTS.md and existing files. If IMPLEMENTATION-STATE.md exists, resume from it.
No prior conversation is required. The specification is the normative contract.

Use C# WPF on .NET 10 LTS, native x64, target Windows 10 build 19041 or newer,
with runtime checks for newer APIs. Pin an installed serviced SDK and packages.
If the required SDK is missing, prepare the files and report the exact build
prerequisite; do not silently downgrade the production target to .NET 8.
Never access game VM memory, inject DLLs, hook DirectX, edit game files, install
a driver, require normal elevation, or promise anti-cheat compatibility.
Do not change physical display/registry/firewall settings while developing.

Create the solution/project boundaries from Section 2. Implement Core models,
typed commands/results/capabilities, immutable reducer and backend planner;
settings/profile/preset/recovery/IPC schemas; atomic versioned JSON stores,
last-good recovery and bounded validation. Initial colors and optional services
are off. Distinguish global matrix scope from per-monitor resources.
Create the WPF shell/navigation and original themes with dependency injection,
accessible names, explicit close-vs-tray behavior and a capability/status model.
Backend implementations belong to later stages; report NotImplemented in this
development checkpoint, never return fake native success.

Define current-user/session authenticated named-pipe contracts, revision ordering,
owned-resource baselines, preview transaction states and recovery-journal types.
Implement the pure reducer including hold as a temporary modifier, stale command
rejection, suspension priority and restore. Provide injectable fake backends for
tests. Add meaningful tests for reducer transitions, corrupt/failed atomic writes,
schema/range validation and migrations. Do not write brittle pixel/layout tests.

Create AGENTS.md describing the safety constraints, module boundaries, expected
build/test commands and requirement to keep durable progress notes. Populate
REQUIREMENTS-TRACEABILITY.md for F01-F38 with current stage/status, not claims.
Build and run non-hardware tests if the SDK is available. Repair errors in scope.
Finish with implemented files/behavior, actual test results, prerequisites and
next stage. Update IMPLEMENTATION-STATE.md with exact unfinished items, contract
decisions and commands. Do not proceed to stage 2 in this turn.
```

### Prompt 2 of 7 — Color engines, native vibrance and recovery

```text
Implement stage 2 of Chromatic Desktop in the existing repository. Read
AGENTS.md, docs/IMPLEMENTATION-STATE.md and specification Sections 3.1-3.4 plus
their F-ID requirements. Treat repository files as durable context; do not rely
on previous chat history. Finish unfinished stage-1 prerequisites first.
Maintain .NET 10 WPF/x64 and the existing interfaces. No game-memory access,
injection, DirectX hooks, drivers or anti-cheat guarantees. Do not mutate the
developer's physical desktop during build/tests.

Implement Chromatic.Native bindings and ABI assertions. MAGCOLOREFFECT is 100
bytes of row-major floats; [R G B A 1] multiplies on the left, translation is
row 5, alpha is preserved. Implement s=percent/100, 0..300%, luma weights
.2126/.7152/.0722, Sij=(1-s)*w[input]+s*delta(input,output). Verify the numeric
300% matrix from the specification. Implement brightness/contrast/warmth,
single composed matrix and the specified approximate color-accessibility mode,
with provenance for simulation coefficients and honest approximation labeling.

Implement the STA Magnification backend: initialize, capture existing matrix,
apply/readback, restore captured baseline, uninitialize; do not change zoom or
input transform. One global matrix is not independent per-monitor saturation.
Implement optional SDR per-output gamma ramps (3x256 WORDs), baseline sampling,
normal gamma, shadows/highlights and bounded Dark Boost f(x)=x+a*x*(1-x/.45)^2
below .45, identity above it, 0<=a<=.8; include basic affine fallback explicitly.
Gamma cannot implement saturation; HDR/ACM and slow/ignored ramps disable it.

Implement optional private NVIDIA DVC using the installed System32 nvapi64.dll,
nvapi_QueryInterface, checked Cdecl delegates and the specification's interface
IDs/20-byte versioned structure. Map the display-driving output, query ranges,
save current level and restore it. Never import SetDVCLevelEx as a named DLL
export or pass a physical GPU handle. Missing/unsupported/invalidated/private
ABI cases degrade to software; no blind interface probing. Separate native DVC
scale from saturation; no accidental double application.

Implement Agent, serialized coordinator, authenticated local pipe, ownership,
atomic journal, UI-liveness shutdown and Recovery companion before any live
display mutation can be enabled. A restore uses the actual baseline and never
has two simultaneous writers. Handle conflicts with other color software.
Wire Display controls and diagnostics to real backend results.

Test math/ABI, monotonic curve/endpoints, composition, failure and crash recovery
with fake adapters/child processes that do not mutate hardware. Add an explicit
opt-in hardware harness with rollback/countdown, but do not run it. Build/test,
fix errors, update F-ID traceability, compatibility gaps and durable progress.
Stop after stage 2 and report real validation rather than claiming GPU support.
```

### Prompt 3 of 7 — Hold input, presets and automatic game profiles

```text
Implement stage 3 of Chromatic Desktop. Read AGENTS.md, IMPLEMENTATION-STATE.md
and specification Sections 3.5 and 3.7, including referenced Core contracts.
Resume prerequisite work before extending it. .NET 10/x64 remains the target.
Use only OS metadata/events, never game VM access, DLL injection, game files,
DirectX hooks, drivers or automated game input. Do not alter live display state
while running ordinary tests.

Implement dedicated pumping-thread WH_KEYBOARD_LL press/up handling and optional
WH_MOUSE_LL Mouse 4/5 holds, strongly retained delegates and unconditional
CallNextHookEx. Callbacks do only bounded state extraction/enqueue and catch
exceptions. Repeats do not retrigger. Chord component release ends the hold.
Use RegisterHotKey with conflict reporting for preset toggles/pause/restore and
screenshot action dispatch; WM_HOTKEY is not a key-up mechanism. Ignore injected
input by default with an explicit accessibility option. Clear hold on focus
loss, lock, suspend, preview, disable or disconnect; reconcile missed release
outside callbacks by polling only configured held keys. Never log typed text.

Implement EVENT_SYSTEM_FOREGROUND SetWinEventHook OUTOFCONTEXT, debounce and
GetWindowThreadProcessId -> OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION only)
-> QueryFullProcessImageNameW; close handles and handle denied/vanished process.
Match normalized exact executable paths, cache with process identity, resolve
own/unknown windows to the configured desktop behavior and avoid PID reuse bugs.
No title-only matching or automatic privilege escalation. Global matrix profile
scope remains all displays; per-output settings retain explicit monitor scope.

Implement local profile/preset CRUD, original built-ins, editable game bindings,
local Steam library/manifest discovery plus manual executable picker. Add typed
Color/Crosshair/Resolution/FullSetup imports/exports, bounded offline share codes
and the project's own chromaticdesk:// link parser/single-instance handoff.
Never contact PlexusX services or use its code namespace. Deep links/imports
show a confirmation diff; unconfirmed resolution changes remain pending until
the display-preview implementation from stage 4. No fake apply success.

Wire Presets/Library and Games pages with preview, reset, duplicate, search/type/
game filtering and preset hotkeys. Save base profile separately from held boost;
release restores the newest base settings. Add tests for rapid focus transitions,
process identity, denied queries, chord/repeat/missed-up, profile-while-held,
malicious and oversized preset payloads and decompression limits.
Build/test, repair in-scope failures, update traceability/progress and list manual
input/GPU checks still required. Stop after stage 3.
```

### Prompt 4 of 7 — Display transactions, HDR and monitor hardware

```text
Implement stage 4 of Chromatic Desktop. Read AGENTS.md, IMPLEMENTATION-STATE.md,
specification Sections 3.6 and 3.11 and restore/ownership invariants in 3.1.
Keep .NET 10/x64 and SDK-generated CCD unions/bitfields. No game injection,
memory access, file modification, drivers, undocumented DWM color transforms
or anti-cheat guarantees. Development tests must not change physical displays.

Implement display enumeration/mapping through QueryDisplayConfig/device-info
packets and EnumDisplaySettingsExW. Keep complete topology, monitor identities,
source vs target IDs and rational refresh. Verify DEVMODEW/CCD/HDR packet sizes
and offsets against the Windows SDK. Retry topology query buffer races safely.
Implement legacy CDS_TEST then temporary CDS_FULLSCREEN path and canonical CCD
SDC_VALIDATE then APPLY path. No persisted mode before Keep, no fabricated
timings, no treating return codes as GetLastError, no dropping other monitors.

Implement supported 4:3 source modes with requested output stretch/scaling,
readback and honest driver limitations; a desktop change cannot force game FOV.
Custom-size selection works only for enumerated sizes by default. Add the
separately enabled public-NvAPI custom-mode provider if qualified, with SDK
structures, trial/revert and save only after Keep; otherwise return Unsupported
with a vendor-settings route. Never use EDID hacks or install custom drivers.

Implement per-target HDR probing, new information type 15 and HDR set type 16
where supported, legacy type 9/10 only for qualified older HDR paths, and no
confusion of ACM/WCG with HDR. Restore SDR effects before HDR; re-query after
every topology change. Mixed HDR/SDR suspends the global matrix by default.
No simulated HDR keyboard shortcut or registry toggle.

Implement compound mode/HDR/scaling preview as a journaled compensating
transaction with independent Agent 15-second deadline, local Keep/Revert,
Recovery already active, stale-queue fencing and rollback after UI death or
hotplug. Only previously confirmed combinations may auto-apply per game.
Wire Resolution view and confirmation UI. Never invoke the hardware harness now.

Implement capability-gated DDC/CI brightness/contrast/RGB gains, handle lifetime,
discoverable ranges, readback, bounded worker queue and explicit persistent Undo.
No arbitrary VCP access. Build event-driven color watchdog with bounded retry/
cooldown and foreign-writer conflict handling. Wire Monitor page.
Test transaction failure at each step, timeout independent of UI, topology
preservation, HDR packet selection, device identity and retry bounds with mocks.
Build/test, update F-ID/status/checklist evidence and exact unqualified hardware
paths in durable state. Stop after stage 4.
```

### Prompt 5 of 7 — Crosshair, capture and the remaining local UI

```text
Implement stage 5 of Chromatic Desktop. Read AGENTS.md, IMPLEMENTATION-STATE.md
and specification Sections 3.8, 3.12 (local UI/integrations), and 3.13.
Keep independent branding, .NET 10/x64 and all existing safety contracts.
No game-state inspection, VM access, injection, DirectX detours, drivers or
guarantee of zero latency/anti-cheat approval. Do not reconfigure the user's
desktop, registry, startup or firewall while developing or smoke-testing.

Implement external unowned WS_POPUP crosshair HWND with LAYERED|TRANSPARENT|
TOPMOST|TOOLWINDOW|NOACTIVATE, no APPWINDOW, premultiplied BGRA top-down DIB
and UpdateLayeredWindow. Keep it small/event-driven, hidden initially, click-
through over opaque pixels, absent from taskbar/Alt-Tab and unable to steal
focus. Implement shapes/dot/gap/thickness/outline/color/opacity, sanitized PNG
images and one shared offset/placement path for drawn and picture crosshairs.
Use physical monitor/client coordinates, PerMonitorV2 DPI, negative coordinates
and hotplug handling. Dispose GDI objects correctly. Hide on suspension or
ineligible game; exclusive-fullscreen support is not promised. Wire the editor.

Implement user-triggered screenshot/clipboard export and recording diagnostics.
Separate raw capture, already-processed capture and explicitly processed preview;
never apply color twice or claim exact reconstruction of native DVC/DDC output.
Use documented capture APIs and their permissions. No continuous game capture,
silent upload or false assurance that OBS/game capture sees every screen effect.
Capture-friendly mode changes backend plan with explicit missing nonlinear features.

Finish original themes, accessible keyboard navigation, bounded local image/GIF
background, window placement, tray/Pause/Restore/Exit and opt-in startup/protocol
registration adapters. Customize opens supported Windows Settings pages by
default; optional version-gated registry compatibility adapters must capture/
restore only requested per-user values and report shell refresh limitations.
No Explorer termination, binary patching or broad privilege workaround.
Implement optional disabled-by-default Discord RPC, local logs/crash diagnostics,
previewable diagnostic export and Legal/licences view. No outgoing report until
an explicit Send to the project's configured endpoint. Cloud remains optional.

Test placement, image limits, alpha math, lifecycle and capture-route decisions.
Use a separate owned test process for click-through/focus checks in an isolated
UI environment; record unrun checks honestly. Build/test, fix errors and update
traceability, manual verification list and IMPLEMENTATION-STATE.md. Stop at 5.
```

### Prompt 6 of 7 — Authenticated phone control and media sessions

```text
Implement stage 6 of Chromatic Desktop. Read AGENTS.md, IMPLEMENTATION-STATE.md
and specification Sections 3.9 and 3.10 plus the shared coordinator contracts.
Continue .NET 10/x64. No game memory/injection/hooks/drivers, no arbitrary remote
execution and no physical display/firewall/network exposure in ordinary tests.

Implement embedded Kestrel Phone server bound only to a user-selected Private
LAN IPv4 address and actual ephemeral port. It starts only while Phone window
is open and stops/invalidates all sessions on close, lock, suspend, app exit or
network change. No 0.0.0.0/public adapter/UPnP/relay. Use local embedded assets.
Implement QR with 256-bit one-time fragment secret, two-minute expiry, local
Allow-phone confirmation, token/ticket authentication, bounded rate limits,
Host and exact Origin checks including WebSocket upgrade, source-prefix scope,
no wildcard CORS, secret-free logs, CSP/no-referrer/no-store and fresh reopen keys.
Plain HTTP/WS is explicitly trusted-private-network-only; it does not protect
against a malicious on-path peer. Do not bypass certificate or firewall prompts.

Implement color-only command whitelist: bounded color sliders and allowed local
scene presets plus state. No resolution/HDR, files, shell, DDC, screenshot,
update or raw native calls. Enforce 4-KiB frames, schema/finite/range validation,
paired sessions, increasing seq, authoritative revision/ack, stale-revision
conflict/rebase, one write in flight with latest-value coalescing, final pointer-
up value, 20-30 Hz cap and two-client limit. Both UIs consume coordinator state.
Write responsive mobile controls and clear connection/capability status. Any
firewall helper is user-invoked, Private/LocalSubnet-scoped and never run now.

Implement GSMTC manager/session picker, current-session option, metadata/art,
capability-gated play/pause/next/previous and seek in 100-ns ticks. Subscribe to
events, handle disappearing/rejecting sessions, no sessions/access denied and
UI-thread dispatch. Verify packaged capability and unpackaged runtime behavior;
report unsupported cases. GSMTC is a controller, not an audio streaming engine,
and exposes no universal volume control. Wire the local Music panel.

Test pairing replay/expiry, malicious Origin/Host, schema/rate/size limits,
session teardown, two-client ordering, reconnect/no stale replay and mock media
failure/lifecycle. Loopback ephemeral integration tests may run without opening
LAN access or changing the desktop. Build/test and update state/traceability.
List real-phone/media/hardware qualification still required. Stop at stage 6.
```

### Prompt 7 of 7 — Integration, release packaging and completion audit

```text
Complete stage 7 of Chromatic Desktop. Read AGENTS.md, IMPLEMENTATION-STATE.md,
the entire specification's requirement inventory and Section 3.14. Audit code
against F01-F38 rather than assuming earlier stages succeeded. Keep .NET 10
WPF/x64, independent branding, no game memory/injection/DirectX hooks/drivers,
and no guarantee of universal anti-cheat, HDR grading or capture parity.

Finish missing supported local utility behavior and real fault paths. Every
feature must be Implemented, Optional, Inactive-by-reference, Unsupported with
reason, or Not yet complete. No hidden TODO or adapter returning fake success.
Verify colors/native DVC/LUT do not double-apply, hold release wins over stale
queued commands, foreground restoration works, display previews survive UI death,
overlay does not steal clicks/focus, phone server tears down, and shutdown/lock/
resume/hotplug restore correctly. No ordinary test mutates physical settings.

Implement per-user installer/upgrade/uninstaller, original protocol and opt-in
startup cleanup, dependency lock/licence notices/SBOM and reproducible publish.
Implement the project's updater with authenticated signed manifest, file hash/
size/architecture checks, expected publisher validation, staging/rollback,
restore-before-update and defer-during-game. No private signing keys in repo.
When real signing/hosting is absent, provide working packaging scripts and an
explicit release prerequisite; do not manufacture a valid production signature.

Keep local mode offline and unlocked. Build optional-cloud contracts and an
independent reference deployment scaffold only if requested/configured: bounded
moderated catalog, own preset codes, OIDC+PKCE loopback sign-in, signed feature
documents and provider-hosted commerce with verified webhooks. Never call
PlexusX APIs or bypass its licensing. The absence of cloud credentials must not
break local utility functions. Do not turn disabled reference update ratings into
telemetry. Do not deploy, purchase certificates, publish or send reports now.

Run the appropriate full non-hardware test suite and isolated smoke/package
checks. Run security-focused input/IPC/LAN/import/update/recovery fault tests.
Fix relevant failures without weakening safety contracts. Do not claim that
passing tests qualifies untested GPU/monitor/anti-cheat combinations. Prepare
MANUAL-TESTS.md and COMPATIBILITY-MATRIX.md with actual Tested/Untested results,
including AMD/Intel/NVIDIA, mixed DPI/multi-monitor, SDR/HDR, 59.94/60/high refresh,
borderless/exclusive limits, phone browsers, DDC and GSMTC. Hardware harness is
explicitly opt-in with active rollback; do not run it on this desktop now.

Deliver the buildable repository, exact build/run/publish instructions, complete
requirement traceability, known limitations, actual test results and remaining
production gates. Update IMPLEMENTATION-STATE.md so another chat can resume
without hidden context. If any required implementation is incomplete, state it
plainly and keep completion status open; do not label a scaffold production-ready.
```

These prompts produce a production-oriented implementation path. Actual production readiness is established by the release gates and hardware qualification, not by wording a prompt or compiling a prototype.

