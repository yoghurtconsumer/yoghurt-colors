# Chromatic Desktop implementation state

Updated: 17 October 2026. Requested work: finish all Stage 3 steps while user is away.
Current checkpoint: Stage 3 implementation complete; manual input/GPU qualification pending.
Previous GitHub checkpoint: 7e69527 (experimental global Rust accent drafts).

## Current outcome

Stage 2 color-engine and process-safety implementation is retained and tested on
the actual .NET 10 Windows x64 target. The solution has nine source projects,
three automated test projects and two distinct manual/process harnesses
(14 projects total). The Stage 3 implementation builds Release/x64 with zero
warnings/errors; 307 VSTest tests pass: 104 Core, 169 integration and 34 UI/launcher.

This is a development checkpoint, not GPU qualification or a release. No physical
display, registry, startup, firewall or LAN setting was changed during development
or automated tests. The hardware harness was built but NEVER run. All native
resource tests inject memory/file adapters. The child-process tests execute the
real host/supervisor logic with test-only adapters.

On 17 October the user reported successful Rainbow Six Siege Fullscreen play on
16 October using the Saturation slider up to 300%, rather than the separate
NVIDIA vibrance control. Record this as user-reported visual success on the known
RTX 3060/BenQ setup. Desktop saturation is therefore not categorically unavailable
in the game's Fullscreen mode. Exact presentation path, build/settings and backend
acknowledgment were not captured, so this does not establish exclusive-scanout,
all-system, capture or anti-cheat qualification. Native DVC remains separately
unverified on hardware. No code change or additional hardware test was made for
this documentation update.

The current request authorizes finishing Stage 3. Its implementation is complete;
manual live-input and GPU qualification remains open. Stop before Stage 4. The
requested Rust extension was clarified: blacken the actual holo reticle and the
white loot hover highlight, not an overlay crosshair or automatic looting.
The supplied TikTok was inaccessible behind verification/login/age gates; none
was bypassed. The later supplied [YouTube mirror](https://www.youtube.com/watch?v=-xwbPDYUWp8)
was visually inspected on 17 October. At about 10 seconds PlexusX shows separate
Hue turn and Darken a colour controls, with 400% colour depth; later frames show
Rust's Accessibility colour menu. This demonstrates the reference controls,
not their exact formula, independent safety certification or identical results.

The user subsequently reported severe whole-screen dark gray / black-and-white
appearance after the 400% blue-holo test, then explicitly requested isolation to
the actual holo and hovered loot only. That cannot be provided by the current
global pixel transform. Both misleading draft shortcuts have been removed.
Generic global hue rotation/darkening controls remain under Advanced global
colour darkening, with explicit scope and high-strength clipping warnings.
Restore followed by resetting the draft is documented; existing saved/imported
values are preserved. No game-element-only solution is claimed. Black holo and
loot isolation remain unsupported requirements. Accepted Siege/Rust presets are
unchanged. The user was advised to Restore; development did not change their
running copy or physical display. Stage 4 work was saved separately while this
feedback was handled.

Three optional local ColorSettings fields default to zero and are omitted at
zero; existing v1 documents load without migration. Strict local settings,
profile, preset, library, automation and IPC schemas accept bounded finite
extensions. The remote ten-control whitelist is unchanged and rejects these
fields. Older binaries reject documents carrying nonzero new fields rather than
silently ignoring them. Save/share/import diff and automatic-rule snapshots
retain the base values; held modifiers remain temporary. Tests cover neutral
preservation, hue/alpha/clipping, invalid ranges/non-finite values, persistence,
IPC, remote rejection, explicit Apply, honest lack of element isolation and exact owned fake-baseline restoration.
Launcher smoke renders/closes without companions/providers. See
[Rust colour review](RUST-COLOUR-DARKENING.md). Stage 4 has not been started.

## Startup regression after switching portable builds

The user's startup report showed AppInstance.ConnectAsync cancellation. Read-only
inspection found the older 111728 portable UI still alive without a window and
without Agent/Recovery companions. App.OnExit synchronously waits for handoff
server disposal; its await captured the blocked WPF context, retaining the pipe.
A deterministic STA/queued-context regression failed before the fix and passes
with ConfigureAwait(false) and finally-protected disposal. The pipe can then be
recreated without starting a second UI or weakening exact-image authentication.

Full-suite testing also exposed unread acknowledgment loss: Windows Disconnect
can discard a completed write before the client consumes it. A delayed-reader
regression reproduced EOF before the fix. The client now confirms receipt using
the existing accepted/challenge frame and waits for server disconnect; the server
waits for the bounded, identity/challenge-checked receipt before disconnecting.
Tests include 32 consecutive handoffs, blocked UI shutdown during a handoff,
foreign-image rejection and actionable timeout errors for an unresponsive copy.
No request is automatically replayed. Show/import remain the only handoff actions.

The WPF and packaged startup smoke paths now create a unique test-scoped pipe and
exercise actual OnExit disposal, while still using in-memory settings and no
companions/native display providers. They never contact the real app endpoint.
The old generic cancellation/image-mismatch popup is replaced with instructions
to exit the older copy; a stuck windowless UI can be ended in Task Manager.
After the full suite, the verified older windowless UI (PID 21632, exact older
portable image path) was removed once after confirming no Agent/Recovery was
running. No game or companion was stopped. No automatic process termination was
added to the product. No game access,
new native imports or Stage 4 work was introduced.

## Stage 3 completion — input, foreground and authenticated link handoff

- Agent owns explicitly opted-in input on a dedicated pumping STA. Low-level
  keyboard and optional Mouse4/5 callbacks are rooted, extract/enqueue bounded
  configured transitions, catch exceptions and always chain. No typed text is
  logged, input suppressed/synthesized or in-game code loaded. RegisterHotKey
  uses MOD_NOREPEAT and reports actual per-ID conflicts. Screenshot is typed
  dispatch with an honest NotImplemented result pending Stage 5.
- Pure HoldTracker rejects repeats/injected events by default, requires all chord
  components, ends on any release and blocks resumed keys until a fresh press.
  Focus/safety/configuration/overflow/disconnect clear hold. A 100 ms poll of only
  configured held/blocked keys can release missed key-up, never activate.
- Basic affine lift composes the base matrix with bounded whole-image lift and
  tint; release uses the newest base. It does not claim shadow-only correction.
  LUT/native-DVC holds remain unavailable until independent latency qualification.
  Journal/readback and revision-plus-generation fencing apply to every write.
  The consumer never waits behind a native completion: bounded pending commands
  let release immediately reduce/fence queued boost work; a gated-write test
  proves release is accepted before the held setter completes.
- Foreground events are OUTOFCONTEXT EVENT_SYSTEM_FOREGROUND only, enqueue HWND
  and event time, debounce 150 ms, verify the current window and query only
  PROCESS_QUERY_LIMITED_INFORMATION path/PID/creation metadata through SafeHandle.
  PID reuse, own UI/Agent, denied/vanished/unknown windows and 500 ms liveness
  fallback are handled. Exact normalized paths use OrdinalIgnoreCase; per-output
  intentions require explicit monitor mapping and global matrix remains global.
  The polling/debounce deadline race found by tests is fixed.
- Games exposes separately saved enabled rules, desktop fallback, hold/chord/tint,
  accessibility/mouse opt-ins and preset/pause/restore/screenshot hotkeys. All
  services default off and saving config never enables effects. automation.json
  v1 is strict, bounded, atomic/last-good; profiles are saved color snapshots and
  base colors stay separate from hold. UI validates IPC size before committing,
  reloads authoritative disk state after failed writes and refuses missing presets.
- chromaticdesk links accept only one validated argv data item. App-to-App handoff
  has a bounded strict schema, fresh challenge, current-user/session local-only
  pipe ACL and real OS peer/exact image/PID/creation pinning on both ends. It
  queues only show or validated preset import, opens Library with a diff and never
  applies. Same-location double launch forwards; the product/user/session endpoint also
  rejects a different executable location before starting a second UI/Agent.
  Tests inject isolated pipe scopes; the Agent writer lease remains a second guard.
  Rejected/early-closing clients reset the pipe even when .NET reports Broken
  (IsConnected=false); the regression verifies later valid handoffs still work.
- Settings exposes explicit per-user URL registration/removal. No registration
  runs on startup. Quoted fixed app command, foreign/partial handler protection
  and own-command-only removal are tested with memory registry adapters; no real
  registry was changed during development. Long OS URLs remain subject to Windows
  limits; offline pasted codes/files remain available.
- Full suite covers fresh press/repeat/chord/injected/missed-up, rapid focus,
  access denial/own windows/PID reuse, overflow, pause, newest base while held,
  actual composed fake matrix/baseline restoration, strict config/future versions,
  interrupted commits/corrupt last-good, argv/foreign app/challenge rejection,
  handoff and UI draft/save/disable workflows. The actual launcher smoke test
  rendered its visible WPF window and exited cleanly with no providers/companions. Existing decompression/malicious
  import and child-process crash/recovery tests remain passing. Input factories
  and display/registry adapters are injected; no real hooks/hardware were used.
- [STAGE3-REVIEW.md](STAGE3-REVIEW.md) records review steps and remaining manual
  input/conflict/focus/session/DPI/hardware checks. Anti-cheat boundary now explicitly
  discloses optional low-level input listeners and limited foreground metadata.

## Historical Stage 3, step 1 — local library and game bookmarks


- Added the user's requested Siege styles Champ (220%, brightness +0.002,
  contrast 0.996) and Im Diamond 1 (190%, brightness +0.005, contrast 0.99).
  Champ builds on the accepted Strong color direction with bolder color and a
  smaller black-level lift. The existing Strong color values are unchanged.
  All six Siege choices are available through Add game presets and name search.
  [Siege notes](SIEGE-PRESETS.md) document exact values and comparison limits.
  These are original styles, not ranked/professional settings or measured game
  visibility improvements. Tests cover neutral detail/alpha/ranges, low lift,
  portable sharing and an upgrade of the prior 19-preset library without replacing
  edited entries or issuing backend commands. No game/hardware test was run.
- The user confirmed the library works, then requested ready-made game looks to
  help compare visibility, and confirmed that pack works before requesting Rust
  biome options. The original seven-preset game pack includes Siege
  Balanced/Indoor lift/Outdoor balance/Strong color, plus balanced starting points
  for Counter-Strike 2, VALORANT and Apex Legends. These are uncalibrated appearance
  choices, not evidence of better enemy detection or game-specific compatibility.
  No game or physical display was used to tune or test them.
- Added five Rust options: Forest / Temperate, Desert / Arid, Snow / Arctic,
  Tundra and Jungle. Biome naming follows the Facepunch-hosted Terrain wiki;
  [Rust preset notes](GAME-PRESETS.md) record source, original values and limits.
  The full game pack now contains 14 presets (21 with the seven scene presets). Add / update Rust presets adds
  missing Rust IDs, refreshes untouched first defaults and selects the Rust
  filter; Add game presets includes missing ones too. Existing customized entries
  and game bookmark IDs are preserved. Neither
  action applies colors or detects the game/biome. Snow/desert lower neutral peak
  whites; Jungle lifts dark neutrals more than Forest, using affine math only.
- User feedback: Siege Strong is the preferred look; the earlier Rust looks
  appeared grey when tried in Siege, and have NOT been tested by the user in
  Rust. Revised Rust saturation to 160–195%, contrast to 0.98–0.996 and neutral
  black output to 0–0.006 (formerly 0.010–0.070). This reduces the large affine
  lift while increasing color separation. Siege Strong is unchanged. No actual
  Rust session or physical display was used to tune or qualify this revision.
- The explicit Rust update action matches the complete known old document
  (including name/tag/metadata/assets), refreshing only exact untouched defaults.
  Renamed/retagged/recolored entries and imported copies are preserved; selected
  reset remains an explicit way to obtain the new original. JSON schema versions
  and bookmark IDs stay unchanged. Tests cover the old palette comparison, fresh
  and repeat refresh, persisted legacy libraries, customization protection and
  preserving the accepted Siege Strong values without issuing backend commands.
- A full-suite run exposed the process harness receiving real Windows display
  notifications during its scripted fake session, causing a stale revision at
  setup. AgentHost now accepts an injectable IAgentBroadcastSource alongside its
  existing injected runtime. The file-backed process harness supplies an inert
  source; AgentDisplayEvents has separate explicit fake-event tests. Production
  always constructs HiddenBroadcastWindow and retains its real safety events;
  bootstrap/IPC provides no event-disable option. No retry of hardware commands
  or weakening of revision/generation fences was added.
- Library's Add game presets action atomically adds missing stable IDs and keeps
  existing presets/edits. It selects the Siege game filter, with other games in
  the Game tag filter. Adding the pack, reset, preview and editing do not apply
  anything. Reset selected starting point restores only that bundled preset;
  duplicate a customized preset first to retain it. The scene reset stays separate.
- The game pack uses saturation 135–220%, gamma=1, neutral shadows/highlights/
  vibrance/CVD and no mode, crosshair, hardware ID or asset. It uses the existing
  global affine SDR route rather than requiring optional LUT or NVIDIA services.
  Neutral gray steps remain monotonic and within output range in mathematical
  tests. Indoor lift raises the whole image; strong saturation may still clip
  colored pixels. Purpose notes are displayed; edited colors are labeled customized.
- Tests cover the gray range/alpha and indoor lift, portable share/store round
  trips, idempotent pack addition, preserving custom names/tags/colors, failed
  atomic pack addition, filter selection and resetting only the selected original.
  New game presets are not inserted into the user's live library during testing;
  the user can add them using the new Library button. Automatic switching remains
  pending and native/game/anti-cheat qualification is not implied.
- Seven original editable starting presets: Neutral, Treeline, Interior, Night,
  Snow glare, Smoke / dust and Bright sky. These are starting values, not game
  performance or visibility claims. Saturation retains its existing 0–300% range.
- Presets/Library now supports create from the Display base draft, rename/tag,
  update colors, duplicate, delete, reset built-ins, and name/author/type/game-tag
  filtering. Preview changes local sample swatches only. Explicit Apply uses the
  existing authenticated Agent path and displays the actual completion/failure.
  Saving never reads effective held boost; per-monitor settings stay separate.
- Strict Color/Crosshair/Resolution/FullSetup JSON imports/exports, .chromaticpreset
  extension and bounded offline CD1 codes. Codes use compact, ordinal-key JSON
  normalized through the typed schema, GZip, base64url and SHA-256. Decompression
  is bounded to 64 KiB; code text is bounded to 48,000 characters. The checksum
  detects corruption and does not establish trust. No hosted catalog is contacted.
- Imports show proposed color/mode/crosshair/asset/author metadata and require
  confirmation to save a new local ID. No automatic application or asset loading.
  Crosshair, resolution and FullSetup Apply is explicitly refused until the later
  stages; mode intent cannot bypass a future Stage 4 preview transaction.
- chromaticdesk://preset/<CD1-code> can be pasted into the Library import box.
  Strict parsing rejects extra routes, credentials, ports, queries and fragments.
  Windows protocol registration and authenticated single-instance argv handoff
  remain pending; clicking an OS link is not claimed as implemented.
- Games supports local executable/preset bookmarks, manual file selection, path
  edits/removal and preview of the chosen Color preset. Missing preset references
  are surfaced. These bookmarks do NOT perform automatic game switching.
- Steam discovery is an explicit button action: HKCU SteamPath READ, registered
  libraryfolders.vdf and top-level appmanifest metadata only. Bounded VDF depth,
  file size, tokens, libraries and manifest count; malformed manifests are skipped.
  No game file scan, executable read, game launch, process inspection or registry
  write. Executables are picked manually; UNC/device/alternate-stream paths fail.
- Separate strict library/games schema v1 stores under the existing per-user data
  directory use flush/atomic replacement and validated last-good storage. Existing
  settings/profile/preset schemas remain unchanged; no migration is needed for the
  new version-1 stores. Corrupt files are preserved, saving is blocked on failed
  load, and failed writes reload authoritative state even after a committed rename.
- Real Release/x64 whole-solution build/test passed on .NET 10.0.402. New tests
  cover all four preset kinds, malicious links/fields/ranges/versions, checksum and
  decompression bombs, corruption/interrupted commits, VDF limits/path traversal,
  CRUD/confirmation/filtering/bookmarks and actual XAML measure/arrange without
  showing windows. All display backends in the tests are injected fakes. The real
  Run-App -NoBuild -SmokeTest launcher also rendered and exited cleanly with
  in-memory defaults, no companions and no native resource providers.

Those historical next steps are now implemented above. Manual qualification is
still pending; the next development stage, after user review, is Stage 4 display
transactions/HDR/DDC. It was not started in this task.

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
  explicitly qualified; Stage 3 now adds an optional basic affine hold; LUT latency qualification remains open.
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
power and unsafe display changes suspend and restore before considering any new plan.
Resolution/refresh notifications preserve only confirmed matrix/native DVC routes
when exact output mappings remain verified SDR; no setter/reapply is issued.
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

At Stage 2, eng/Test.ps1 -Locked restored locked dependencies, built the whole
solution and ran 186 tests. Six isolated child-process scenarios cover graceful
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

## Follow-up: game fullscreen transitions

The user reports Rainbow Six Siege works in Borderless but loses color in Fullscreen,
and colors do not return on Alt+Tab. The prior display-event handler unconditionally
suspended/restored on WM_DISPLAYCHANGE, which can be caused by resolution changes.
AgentDisplayEvents now uses an exact-mapping/SDR/confirmed-observation policy to
preserve simple global matrix and native DVC plans. It never replays native writes,
recaptures baselines or clears another suspension on that shortcut. Unconfirmed,
failed, gamma/tonal, HDR/unknown/mapping-changing cases retain suspension/restoration;
failed safety completion now tears down the Agent through its existing recovery path.

This fixes Chromatic's own unwanted disable path, not an exclusive-fullscreen
presentation bypass. The Display page explains that limit and has a draft-only
NVIDIA preparation action, with explicit opt-in/Save/restart/output selection and
actual backend-result instructions. Native uses neutral saturation and separate
vibrance; failed private calls report software fallback. No claim of fullscreen
coverage follows from software fallback. Tests exercise repeated notifications
through the actual event handler with a fake backend, safety suspensions, pending/
failed/gamma exclusions, native-versus-software planning, no double vibrance and
exact DVC restoration. The preparation action sends no IPC or hardware command.

Read-only GPU metadata reports RTX 3060, driver 32.0.16.1692. This is inventory,
not a tested support listing. No game process/window/memory or files were accessed;
no private NVIDIA setter, physical display change or game input was performed in
development/tests. The user closed Chromatic so its binaries could be replaced.
Game/fullscreen/NVIDIA visual and anti-cheat qualification remain unperformed.

## Private friend-test packaging (explicit follow-up request)

eng/Publish-Portable.ps1 builds an optimized PortableTest configuration so a live
Release application is not replaced. It publishes App and each fixed sibling
Agent/Recovery separately, self-contained win-x64 on the unchanged .NET 10/Windows
19041 target. SDK 10.0.402 selects bundled .NET/Desktop/ASP.NET Core 10.0.13;
includedFrameworks versions are checked. Restore stays locked. No SDK/runtime
installation, administrator privileges, driver redistribution or security-setting
changes are required on the tester's machine.

The ZIP includes a private-testing read-me, original project licence status,
runtime terms/notices, SDK/generated-binding provenance, build/source/runtime
metadata and file/archive SHA-256 hashes. It excludes personal settings/logs,
recovery journals, test binaries/harnesses, PDBs and nvapi64.dll. Verification
metadata includes only relative component names, not this developer's paths.
The private test request does not choose a public source licence, publish a
public release or complete Stage 7 signing/installer/updater/SBOM/security gates.

All three packaged executables passed real startup checks with PATH restricted
to Windows and DOTNET_ROOT pointing to a nonexistent directory. The loaded
CoreLib path was verified under each executable's own directory. App renders and
closes in no-provider smoke mode; companion smoke modes return before bootstrap,
pipes or providers. Final ZIP entries are decompressed and SHA-256 checked against
the verified folder. This is not a clean-machine VM or physical hardware test.

During Stage 2 packaging, a whole-solution Release rebuild was blocked by the user's live App
holding DLLs. It was not terminated. Whole-solution Debug/x64 build and 186 tests
passed instead; optimized self-contained publishing and three independent runtime
startup checks passed separately. No product target was downgraded. The earlier
Release/x64 186-test checkpoint is historical; the current Stage 3 library
checkpoint above passed a full Release/x64 build and all 240 tests.

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
- Optional live input is implemented; no real hook/game/GPU qualification was run.
  LUT hold remains blocked pending latency and on-screen grayscale checks.
- Hotplug/mode reset/hybrid mapping and private NvAPI support require an explicit
  driver matrix. Unknown/HDR/ACM states suspend global SDR effects.
- Mode/HDR transactions, DDC, overlay/capture, phone and media remain pending in
  Stages 4–6. Stage 3 input/foreground and local authenticated app-link listeners
  are explicit opt-ins/default off except the local single-instance UI pipe.
  No network listener, automated gameplay or physical qualification is added.
- Signing, packaging/publish companion layout, installer/updater, licence
  selection, full dependency notices/SBOM and final security audit remain Stage 7.
  Do not describe the final Section 2 file tree or release gates as complete.
