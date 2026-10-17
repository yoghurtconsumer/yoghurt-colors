# Chromatic Desktop

Independent Windows 10/11 display utility. Stage 3 adds a local preset library, exact-path game profiles,
optional hotkeys/temporary hold and authenticated preset-link handoff to guarded
color engines, authenticated Agent IPC and independent recovery. Production is C# WPF,
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

In **Presets / Library**, choose a starting preset and **Preview in Display**, or
adjust Display and **Save Display draft as new** with a name/game tag. Preview
does not change the desktop. **Apply selected colors to desktop** requests the
existing guarded color engine and shows its actual result. Rename, update,
duplicate, delete, search/type/game-tag filters and reset of original starting
points are available. Color presets save base colors; monitor intentions and
temporary held boost are separate.

For ready-made game looks, click **Add game presets** in Library. This adds
missing presets and keeps your existing edits. Siege has **Champ**, **Im Diamond 1**,
**Balanced**, **Indoor lift**, **Outdoor balance** and **Strong color**; the Game tag filter also lists
CS2, VALORANT and Apex starting points. Select a preset, preview, then explicitly
Apply. **Reset selected starting point** restores that one original; duplicate
first to keep a customized version. Indoor lift changes the whole image. These
are original uncalibrated appearance choices; map/display comparison is still
needed. They do not detect enemies. Automatic game selection requires a saved, enabled rule below.

**Champ** uses saturation 220% with near-neutral contrast and a tiny black-level
lift; **Im Diamond 1** uses 190% with a little more lift. Add the game pack, search
for the name, preview and explicitly Apply. Their values and comparison notes are
in [Siege preset notes](docs/SIEGE-PRESETS.md). Strong color remains unchanged.

For Rust, click **Add / update Rust presets** to add its five options and show
the Rust filter: **Forest / Temperate**, **Desert / Arid**, **Snow / Arctic**,
**Tundra** and **Jungle**. The full game-pack button includes them as well. Choose
the environment yourself; Preview/Apply remain manual. Values and intended
differences are in [Rust preset notes](docs/GAME-PRESETS.md). Rust now uses richer
color and a much smaller black-level lift. Untouched earlier Rust defaults update
with this button; customized presets and imported copies stay intact. Use Reset
selected starting point to refresh one customized original after duplicating it
if you want to keep both. Siege Strong retains its user-preferred values.

Export `.chromaticpreset` JSON or generate an offline **CD1** share code. Paste a
code or `chromaticdesk://preset/<code>` into the import box, review the diff, then
**Confirm import** to save a new local copy. Importing never applies it. Resolution,
crosshair and full-setup documents can be stored/shared but application awaits
their later stages. In Settings, **Register preset links** explicitly registers
this app location for your user. OS links open the running app and show the same
confirmation diff. Nothing registers during startup; foreign handlers are preserved.

**Games** saves local executable/Color-preset bookmarks. **Read installed Steam
games** explicitly reads registered local Steam library/manifests; choose the
executable manually. Select a saved bookmark, **Draft automatic rule for selected**,
check that rule, opt in to automatic profiles and **Save automation configuration**.
Effects still start off; explicitly Apply once before expecting colors to follow
foreground games. Unknown/denied processes and Chromatic's own window use your
desktop fallback. The global matrix affects every display. Rules use saved preset
snapshots: save automation again after editing a library preset.

The same panel offers conflict-aware Ctrl/Alt/Shift hotkeys and an optional
keyboard/Mouse4/Mouse5 temporary hold. Hold works only for enabled game rules,
requires fresh presses after focus/safety changes, and releases to the newest
base look. It uses a basic whole-image affine lift; LUT/native-DVC hold remains
unavailable until latency qualification. Screenshot dispatch honestly reports
NotImplemented until Stage 5. Input listeners run in Chromatic's own process,
never suppress/synthesize input, and are off by default. Foreground matching
queries only process path/PID/start metadata with limited rights; it does not
read game memory. See [Stage 3 review steps](docs/STAGE3-REVIEW.md).

Data lives in `%APPDATA%/ChromaticDesktop/library.json`, `games.json` and
`automation.json`, with atomic replacement and validated `.last-good` backups.
Bookmarks never launch games. There is no game input automation, reticle
recognition, automatic biome detection or anti-cheat certification.

Gamma compatibility and private NVIDIA DVC require separate Settings opt-ins,
Save and restart. Gamma/shadows/highlights need an explicit output. Multiple active
gamma outputs are blocked pending shared-ramp qualification; ignored or slow LUTs
disable the session's route. HDR/ACM/unknown mappings refuse SDR grading.
Native DVC remains experimental and can fall back to software after safe restore.
CVD correction and software vibrance are explicitly approximate.

For exclusive fullscreen games on NVIDIA, the desktop saturation matrix may be
bypassed. To try the separate experimental driver vibrance route:

1. In **Settings**, opt in to **experimental private NVIDIA DVC**, then Save and restart.
2. In **Display**, press **Prepare NVIDIA vibrance (resets draft)**. This selects
   the output when there is only one and prepares saturation 100%, vibrance 75/100
   with other controls neutral; it applies nothing yet.
3. Select the intended output and press Apply. The result must name
   **Magnification+NvidiaDvc**. A **Magnification** result means software was used
   and does not establish exclusive-fullscreen coverage. Adjust Vibrance, not
   Saturation, for this route. The driver limit differs from 300% software saturation.

Confirmed matrix/native DVC effects now remain enabled across display notifications
when the same output mappings remain verified SDR. Unknown/HDR/topology changes,
pending/failed applies, gamma/tonal routes, lock and sleep retain the restore path.
There is no repeated reapply or attempt to override another utility. Driver resets
and fullscreen-visible output still need a human check; no fullscreen guarantee.

If Apply reports HDR/advanced color, open **Windows Display settings** from the
Display page, select your monitor, open **HDR**, and turn **Use HDR** off yourself.
If advanced color remains active, check **Display → Color profile → Automatically
manage color for apps** where available. Return to Chromatic and press Apply;
restart the app if Windows has not delivered a display-change notification.
Chromatic never changes these Windows settings automatically. Restore/exit before
returning to HDR content. HDR switching is separate from HDR color processing.

Apply now shows connection/safety reasons and progress beside saturation. It sends
one profile transaction and reports success only for completed, current readback.
Optional tones are disabled until the gamma route is available; **Reset optional
tones** preserves global saturation. Monitor selection survives status refreshes.

On an unfinished journal, the app offers local baseline recovery before starting
a new writer. Foreign/conflicting values and corrupt journals are preserved for
review. Session/companion logs are at
%LOCALAPPDATA%/ChromaticDesktop/sessions/<sessionId>/.

A safe launcher check is **Run-Chromatic.cmd -SmokeTest**: in-memory defaults,
no companions/providers, first rendered frame, automatic close. No administrator
rights or permanent PATH change are needed.

For a metadata-only report after building, run the app executable with
**--diagnostics**. It writes
%LOCALAPPDATA%/ChromaticDesktop/logs/read-only-diagnostics.json without starting
companions, initializing Magnification or applying colors. It includes per-output
raw color flags and the modern active mode where supported. On 16 October the
legacy probe falsely blocked a BenQ SDR display (legacy flags 5). The fixed probe
uses Windows 11's explicit active mode (modern flags 69, SDR), with a runtime gate
and conservative legacy fallback on older Windows. No Windows setting was changed.

Display's **Advanced global colour darkening** controls affect matching colours
throughout the desktop. They cannot isolate a Rust holo reticle or loot highlight.
The old holo/loot test shortcuts were removed after the user reported severe
whole-screen darkening. Use **Restore**, then reset colour darkening / hue rotation
before applying another look. [Scope and limits](docs/RUST-COLOUR-DARKENING.md).

**Rust — Vivid clarity** is a new portable starting look with richer colour and a
modest global lift for shaded areas. [Import it in your current build](docs/RUST-VIVID-CLARITY.md);
it does not remove game shadows or change existing presets.

Stage 4 adds **Resolution → Refresh monitors / Load supported modes / Review
mode plan** (read-only), plus an explicit **experimental SDR source-size/scaling
preview**. Enable it in Settings, Save and restart, then Preview a supported
source-size option at the current target timing. A global Keep/Revert banner and
Escape accompany an Agent-owned 15-second deadline and independent Recovery.
Colours remain off until you Apply them again. Physical refresh changes, HDR,
custom timing and DDC are still unavailable. Native GPU/driver qualification and
the Windows SDK compile-only ABI check remain open; this opt-in is off by default.
[Review instructions and limits](docs/STAGE4-REVIEW.md).

## Build and automated tests

For an invited friend who has no SDK or .NET installed, create a private Windows
x64 ZIP with:

    powershell -NoProfile -ExecutionPolicy Bypass -File eng/Publish-Portable.ps1

Send the entire ZIP printed by the script. The friend extracts it and opens
**Chromatic.App.exe**; no SDK, separate .NET installation or administrator rights
are needed. App, Agent and Recovery each include .NET 10.0.13. Keep every DLL and
the companions folders. This is an unsigned private test package, not a public
release. It includes READ-ME-FIRST.txt, runtime/dependency terms, build information
and SHA-256 checksums. No personal settings, recovery journals, driver DLLs or
test harnesses are packaged. The source's project licence status remains unchanged.

The script uses a separate optimized PortableTest configuration, checks all three
apphosts with SDK/runtime environment paths removed, and verifies each loaded its
own bundled CoreLib. App smoke mode renders/closes without companions; companion
smoke modes return before bootstrap/providers. Every archived file is hash-checked
against the verified folder. These checks establish startup/package integrity,
not GPU/game/anti-cheat qualification. Windows 10 build 19041+/11 x64 is required.
The last generated ZIP path is in artifacts/latest-private-test-package.txt.

Microsoft SDK **10.0.402 x64** and .NET/Desktop/ASP.NET Core **10.0.13** are
installed in %LOCALAPPDATA%/ChromaticDesktop/dotnet. The helpers select that
installation for their process. global.json pins it exactly; the Windows target
is net10.0-windows10.0.19041.0 / win-x64.

    powershell -NoProfile -ExecutionPolicy Bypass -File eng/Test.ps1 -Locked

Or in PowerShell:

    . ./eng/Use-DotNet.ps1
    dotnet build ChromaticDesktop.sln -c Release -p:Platform=x64
    dotnet test ChromaticDesktop.sln -c Release -p:Platform=x64 --no-build

Verified: **14 projects, zero build warnings/errors, 357 passing VSTest tests**
(107 Core, 213 integration, 37 UI/launcher). Current TRX results are under each automated test project’s TestResults folder.
Tests cover math/ABI, strict schemas/migrations, atomic write failures, owned
baselines, stale revisions, real OS-authenticated pipes and independent
child-process cases, including Display Apply, mode timeout, UI death, Agent crash
and hung setter recovery.
Those processes use file-backed fake resources.
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
drivers or anti-cheat guarantees. See the reviewed
[anti-cheat boundary](docs/ANTI-CHEAT-BOUNDARY.md); game policies still apply.

Input/game profiles and library/imports are implemented in Stage 3; full display/HDR/DDC
transactions are partially implemented in Stage 4; overlay/capture Stage 5; phone/media Stage 6.
Deferred providers remain honest NotImplemented. No phone/LAN listener, registry startup,
cloud, Discord or updater service is enabled. Packaging/signing/licensing are
Stage 7. See [capabilities](docs/CAPABILITIES.md),
[compatibility evidence](docs/COMPATIBILITY-MATRIX.md),
[security](docs/SECURITY.md) and [dependency provenance](docs/THIRD-PARTY-DEPENDENCIES.md).
