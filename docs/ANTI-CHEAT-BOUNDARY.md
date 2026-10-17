# Game and anti-cheat boundary

Reviewed 17 October 2026 against Stage 3 input/profile/link and Stage 4 experimental SDR display preview. No game or anti-cheat
was launched for this review. This is an architecture review, not certification
or a promise that a game will permit Chromatic.

The default color route calls Windows Magnification on the desktop. The optional
local hue rotation/colour-darkening extension composes the same global matrix;
it adds no native imports, game access, capture or object/hover recognition. Gamma is a
separate opt-in Windows display API; private NVIDIA DVC is experimental and off
by default. These are display operations. The application does not access game
memory, inject a DLL, attach a debugger, intercept DirectX, edit game files or
install a driver. Crosshair overlay and phone listeners remain unavailable.
Resolution inventory uses EnumDisplaySettingsExW and CCD queries. Experimental
SDR source-size/scaling previews add SDK-generated User32 SetDisplayConfig to
the reviewed entry-point allowlist. Agent validates then applies only an explicit
local preview; verified-death Recovery uses the same API for owned baselines.
This changes Windows desktop display configuration, with no game-process access,
input synthesis, hook expansion or saved display-database write. The opt-in is off
by default. HDR/DDC setters and automatic game-mode switching remain unavailable. The Vivid clarity preset uses existing global affine display controls.
Local game bookmarks are data. Automatic foreground matching is a separate,
explicit opt-in: Agent uses EVENT_SYSTEM_FOREGROUND with OUTOFCONTEXT, querying
only path/PID/creation metadata via PROCESS_QUERY_LIMITED_INFORMATION. Denied
access falls back to desktop; it never escalates or requests game VM rights.

Hold is a separate explicit opt-in: dedicated Agent pumping-thread
WH_KEYBOARD_LL and optional WH_MOUSE_LL callbacks execute inside Chromatic,
retain their delegates, enqueue only configured key transitions and always chain
without blocking input. Discrete hotkeys use RegisterHotKey/MOD_NOREPEAT, with
actual conflicts reported. Injected hold input is ignored unless the user opts
into accessibility input. No typed text is logged. Reconciliation polls only
configured held/blocked keys and can release, never activate. These optional
listeners expand the old library-only behavior; they are not anti-cheat approval.

Steam discovery runs only on an explicit button click and reads registration,
library and manifest metadata. It does not read game executables, recursively
scan game directories or launch games.
Library CRUD/import/share operations are local files and bounded JSON, without
game-process access. Deferred features remain marked unavailable. Preset OS links are bounded data,
OS-authenticated to the exact app image/user/session, with confirmation before
import and a separate explicit Apply. Only the Settings registration button
writes the app-owned per-user URL handler; no registry writes run in tests.

Process access authenticates the known UI/Agent/Recovery children and pins their
OS handles. It uses limited query rights and synchronization; termination rights
are confined to Recovery's verified fixed-image Agent. It requests neither game
VM rights nor PROCESS_ALL_ACCESS. IPC exposes typed effects, never arbitrary
pointers, code, DLL names or process operations. System32-only loading of the
already-installed nvapi64.dll happens only when the private route is opted in.

NativeBoundaryTests reflects the compiled Native and Agent imports (including
generated CCD imports) and rejects entries outside the reviewed display, pipe,
process-identity, optional input/foreground and session-notification allowlist. It also checks Core's pure
assembly boundary. This catches accidental expansion of those imports; it does
not inspect Windows/framework internals or prove a vendor's compatibility.

Game rules and vendor decisions still govern whether color/visibility utilities
are allowed. BattlEye's [official FAQ](https://www.battleye.com/support/faq/),
last checked on 17 October 2026, describes general support for non-cheat visual tools
subject to the game developer's decision. This is not approval of Chromatic or
every effect. No exclusion, anti-cheat bypass, security disabling, process hiding
or evasion is implemented or recommended. Check the particular game's rules;
close Chromatic if its use is prohibited.

Public Windows API reference:
[MagSetFullscreenColorEffect](https://learn.microsoft.com/en-us/windows/win32/api/magnification/nf-magnification-magsetfullscreencoloreffect).
Hardware-visible behavior, fullscreen presentation and game-specific policy remain
separate manual qualification work. Readback and fake tests cannot establish them.
