# Chromatic Desktop implementation state

Updated: 16 October 2026. Requested checkpoint: Stage 2, followed by launcher/Apply fixes.
Previous GitHub checkpoint: 1e26ea8 (Stage 2 guarded color engines and recovery).

## Current outcome

Stage 2 color-engine and process-safety implementation is present and tested on
the actual .NET 10 Windows x64 target. The solution has nine source projects,
three automated test projects and two distinct manual/process harnesses
(14 projects total). Release/x64 builds with zero warnings/errors; 166 VSTest
tests pass: 58 Core, 94 integration and 14 UI/launcher tests.

This is a development checkpoint, not GPU qualification or a release. No physical
display, registry, startup, firewall or LAN setting was changed during development
or automated tests. The hardware harness was built but NEVER run. All native
resource tests inject memory/file adapters. The child-process tests execute the
real host/supervisor logic with test-only adapters.

Stop here after Stage 2. Stage 3 input/game/library work is not authorized by the
current next-phase request.

## Target and dependencies

Production remains C# WPF, net10.0-windows10.0.19041.0, win-x64,
PlatformTarget=x64, Windows 19041 minimum. There is no .NET 8 product fallback.
SDK 10.0.402 and .NET/Desktop/ASP.NET Core 10.0.13 are installed in
%LOCALAPPDATA%/ChromaticDesktop/dotnet. The Microsoft archive was SHA-512 verified.
The original Program Files .NET 8 installation and permanent PATH are unchanged.
global.json pins the exact SDK. Use eng/Use-DotNet.ps1 or the launcher/test helpers.

CsWin32 0.3.363 is a private build dependency in Native; its generated CCD unions,
inline arrays and bitfields live under obj/Generated. All transitive versions
and hashes are locked. Only read-only CCD APIs are requested at this stage.
No handwritten guesses for CCD unions or newer HDR setters were added.
See THIRD-PARTY-DEPENDENCIES.md and COLOR-PROVENANCE.md for attribution and open
release-licensing work. DI still uses the ASP.NET Core shared framework.
The historical isolated .NET 8 probe is narrowed to Core, persistence and pure IPC
framing. Its 75 checks also passed on the isolated .NET 8 source probe; those
checks cannot qualify WPF/native/companions or substitute for the .NET 10 tests.
An online NuGet vulnerability audit, including transitive packages, reported no
known vulnerable packages on 15 October 2026.

## Implemented foundation retained from Stage 1

The original specification and build prompts were moved unchanged into docs.
All nine source projects and the Stage 1 test projects exist. Core is pure;
typed settings/profiles/presets, reducer, temporary holds, backend planner,
strict ranges and six schemas remain in place. Infrastructure owns atomic JSON
storage, bounded JSON/framing, migrations, repositories and recovery journals.
Corrupt settings can load validated last-good data without overwriting damage.
Unknown fields, duplicate keys, nonfinite/out-of-range values and future schema
versions are rejected. Holds are never saved into a base profile.

The WPF shell retains eight navigation pages, Music panel, Default/Slate themes,
explicit close-to-tray opt-in, exit/restore actions and startup diagnostics.
Run-Chromatic.cmd builds the app directly and waits for its first rendered window.
Failures remain visible with a log path. Tests cover paths with spaces, unrelated
working directories, an older SDK first on PATH and a missing executable.
Smoke mode uses in-memory defaults, launches no companions and closes after render.

## Stage 2 color and native implementation

- Row-vector 5x5 saturation/affine matrices, 0–300%, software vibrance cap,
  preserved alpha, row-five translation and one composed global matrix.
- Experimental red/green/blue-weak CVD correction, adjustable strength, attributed
  Machado severity-1 coefficients and original correction code. Encoded-SDR
  approximation is disclosed; no medical or exact simulation claim.
- Bounded monotone gamma/shadow/highlight/Dark Boost curves, deterministic
  ordering, tint bounds, exact neutral mapping, baseline calibration sampling
  and exact 1536-byte gamma restoration. Basic affine lift math is explicit.
- MAGCOLOREFFECT (100 bytes), gamma ramp (1536 bytes), DVC (20 bytes), CCD
  packet sizes/offset checks. Magnification initializes/reads/writes/uninitializes
  on one STA, never touches zoom/input transforms and declines an existing
  non-neutral global filter rather than fighting another owner.
- Gamma uses a display-specific DC, monotone captured baselines, SDR/mapping
  checks, readback and measured setter latency. Ignored/failed ramps and writes
  exceeding 250 ms fail the plan, restore captured baselines and disable the
  session's LUT route. Hold support remains unavailable until latency is
  explicitly qualified; Stage 3 has no input implementation yet.
- Optional private NVIDIA DVC loads only installed System32 nvapi64.dll, known
  QueryInterface IDs/Cdecl delegates, checked statuses/version/ranges and the
  GDI display-driving handle. Its 0–100 intention has 50 neutral and uses
  discovered asymmetric ranges. It never substitutes a physical GPU handle.
  Incompatible outputs fall back to software only after safe route restoration.
- PlannedColorBackend restores the departing plan before applying another.
  Software vibrance is omitted from the matrix when native DVC owns vibrance.
  Native plus extra saturation is conservatively routed to software in this UI;
  the pure planner retains the explicit advanced-combination contract.
- Read-only, bounded/retried CCD inventory validates device-path/GDI/LUID/target
  mappings and advanced-color state. Windows 11 build 26100+ uses the generated
  ColorInfo2 packet's active SDR/WCG/HDR mode; older Windows uses legacy flags.
  Unknown, active HDR/ACM, clone/ambiguous
  mappings fail closed. No mode/HDR setter is generated or used.

## Stage 2 ownership, IPC and recovery

Agent owns a pumping STA and a hidden top-level broadcast window. Session lock,
power/display changes suspend and restore before considering any new plan.
Automatic reapply/resume is deferred to Stage 4's bounded watchdog; an explicit
Apply can resume after fresh SDR checks.

The reducer accepts commands independently of native/disk awaits. Immutable
revision AND generation fences are rechecked after journal flush immediately
before every prospective write. Both direct and real-pipe regression tests prove
that Disable invalidates a pending write, and a stale failure cannot roll back a
newer successful apply.
Native adapters recheck that authorization after their final SDR/output preflight;
the DVC wrapper also checks after its final range query. A regression superseding
the command inside the SDR probe proves that no native setter is reached.

OwnedResourceController captures actual baselines, journals last confirmed and
pending values atomically BEFORE setters, verifies readback, and restores exact
captured values. Resource identity is stable monitor path/EDID; runtime mappings
are freshly checked. Foreign values latch a conflict instead of being overwritten.
A failed plan rolls back owned resources; failed rollback blocks further effects.
Recovery refuses older last-good journals because they could omit the last
captured resource. Corrupt/conflicting journals are preserved for local review.

App starts fixed sibling Agent/Recovery executables. A bounded, compact bootstrap
with a random 256-bit nonce travels only over inherited stdin, not argv/disk.
Pipes have a current-user-only DACL, first-instance protection and
PIPE_REJECT_REMOTE_CLIENTS. OS pipe PID, token SID/session and process creation
time are verified before nonce exchange. Process image/identity checks pin real
OS handles. The Recovery channel permits only GetState, Restore and Shutdown.
Frames are bounded to 64 KiB; up to 32 correlated requests can run concurrently.
One reader and serialized frame writes prevent interleaving. Timeouts close the
connection instead of reusing an uncertain frame boundary.

Agent monitors the pinned UI and Recovery handles and restores/shuts down on
disconnect or unexpected death. Recovery's one-second heartbeat must pass through
the actual STA (three-second response limit). On a hung Agent it requests IPC
shutdown first, allows three seconds to exit, then may terminate only the verified
fixed-image Agent. It waits for the pinned process to signal before acquiring the
exclusive writer lease and restoring. PID reuse, a boolean desired-state flag,
or pipe loss alone is never treated as ownership transfer.

On next normal launch App offers recovery of an unfinished journal before
starting a new writer. Recovery-only mode verifies the previous process identity
has ended and refuses access-denied/ambiguous proof. It does not overwrite foreign
values or automatically use a backup. Logs and session files are under
%LOCALAPPDATA%/ChromaticDesktop/sessions/<sessionId>/.

## Display UI and safe testing

The Display page provides global saturation/brightness/contrast/warmth,
separate vibrance scale, approximate CVD mode/strength, gamma/shadows/highlights
and explicit output selection/per-output LUT warmth. Sliders preview local
neutral-calibration samples. Apply uses typed authenticated commands and displays
actual completion/error results. No slider automatically writes hardware.
Effects always start off; successful Apply updates only base settings in memory,
which Settings/Save persists. Compatibility services require opt-in, Save and
restart. The UI is shown before asynchronous companion connection/recovery,
so a failed companion cannot reproduce a silent launcher flash.

eng/Test.ps1 -Locked restored locked dependencies, built the whole solution and
ran the 166 tests above. Six isolated child-process scenarios cover graceful
exit, crash after setter/before confirmed journal, a hung setter, UI death and a
foreign writer, and DisplayViewModel Apply through authenticated Agent IPC with
exact expected matrix/readback and baseline restoration. These use the real
AgentHost/RecoveryHost with JSON-file resources.
Native API wrappers, ABI, OS pipe authentication, restricted roles, slow gamma,
journal corruption, schema/range validation, migration and first-render launcher
behavior have automated coverage.

## Follow-up: silent Apply and launcher diagnostics

The user reported no color change and no message on Apply. A metadata-only probe
initially reported advanced color from legacy flags=5 on build 26200. Direct
inspection of Windows Settings showed HDR unsupported on the BenQ XL2540X+ and
automatic color management already off. A new read-only ColorInfo2 query returned
flags=69, activeColorMode=SDR, 8 bits/channel. The legacy bit-2 interpretation was
therefore falsely blocking this machine. The fixed runtime-gated probe uses the
modern active mode, ignores support/user-enabled/reserved bits as evidence of
activity, and still blocks WCG/HDR/unknown modes. The updated local diagnostic
reports Known=true, AdvancedColorActive=false, AllSdr=true. No Windows setting
was changed; no physical color apply or hardware harness was run.

Apply/result/connection reasons are visible at the top of Display. Clicking an
unavailable route explains why. One typed ApplyProfile atomically sets the base
profile, enables effects and releases only user Pause; UI IPC cannot clear OS
safety suspensions. OS resume clears its corresponding reason but leaves effects
off until explicit Apply. Progress, pipe failures and superseded acknowledgments
are explicit; only current successful readback updates saved base intent.
Optional gamma tones are disabled without the required route, with a reset path
that preserves saturation. Stable monitor selection survives polling and remaps.

The launcher captures both native output streams under Windows PowerShell 5,
preflights the exact pinned SDK and preserves the complete SDK/build error. A
fixture with an unavailable SDK verifies multiline stderr and installation help.
The real build-and-smoke command also renders and exits without companions or
hardware writes. --diagnostics records runtime, companion files and per-output
metadata without initializing Magnification. NativeBoundaryTests checks compiled
imports and Core separation. See ANTI-CHEAT-BOUNDARY.md; no vendor/game approval
or zero-false-flag promise is made. Later-stage features remain deferred.

Chromatic.HardwareHarness is a separate manual-only executable. Two explicit
flags, an interactive APPLY acknowledgment, five-second countdown and ten-second
automatic baseline rollback are required. It uses ordinary Agent/journal/recovery
and has no native bypass. It was NOT executed. See MANUAL-TESTS.md.

## Remaining compatibility and release gates

- Actual AMD/Intel/NVIDIA/driver/OS/monitor and capture behavior remains untested.
  API readback alone never qualifies visible output or capture parity.
- Multi-output LUT writes are conservatively blocked as SharedRampUnqualified:
  drivers may share ramps. Safe independent-output/alias qualification remains
  open. Single-output gamma is optional and can still be refused by the driver.
- No live hold input or automatic latency qualification is exposed. Fast/ignored
  driver behavior and on-screen grayscale checks remain manual qualification.
- Hotplug/mode reset/hybrid mapping and private NvAPI support require an explicit
  driver matrix. Unknown/HDR/ACM states suspend global SDR effects.
- Complete mode/HDR transactions, DDC, input/game profiles, libraries/imports,
  overlay/capture, phone and media are Stages 3–6; their providers remain honest
  NotImplemented. No listeners outside local authenticated named pipes exist.
- Signing, packaging/publish companion layout, installer/updater, licence
  selection, full dependency notices/SBOM and final security audit remain Stage 7.
  Do not describe the final Section 2 file tree or release gates as complete.
