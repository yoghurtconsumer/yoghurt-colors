# Game and anti-cheat boundary

Reviewed 16 October 2026 against the Stage 2 implementation. No game or anti-cheat
was launched for this review. This is an architecture review, not certification
or a promise that a game will permit Chromatic.

The default color route calls Windows Magnification on the desktop. Gamma is a
separate opt-in Windows display API; private NVIDIA DVC is experimental and off
by default. These are display operations. The application does not access game
memory, inject a DLL, attach a debugger, intercept DirectX, edit game files or
install a driver. There is no implemented crosshair overlay, game scanner, global
input hook or phone listener in this checkpoint. Deferred features remain marked
NotImplemented.

Process access authenticates the known UI/Agent/Recovery children and pins their
OS handles. It uses limited query rights and synchronization; termination rights
are confined to Recovery's verified fixed-image Agent. It requests neither game
VM rights nor PROCESS_ALL_ACCESS. IPC exposes typed effects, never arbitrary
pointers, code, DLL names or process operations. System32-only loading of the
already-installed nvapi64.dll happens only when the private route is opted in.

NativeBoundaryTests reflects the compiled Native and Agent imports (including
generated CCD imports) and rejects entries outside the reviewed display, pipe,
process-identity and session-notification allowlist. It also checks Core's pure
assembly boundary. This catches accidental expansion of those imports; it does
not inspect Windows/framework internals or prove a vendor's compatibility.

Game rules and vendor decisions still govern whether color/visibility utilities
are allowed. BattlEye's [official FAQ](https://www.battleye.com/support/faq/),
checked on the review date, describes general support for non-cheat visual tools
subject to the game developer's decision. This is not approval of Chromatic or
every effect. No exclusion, anti-cheat bypass, security disabling, process hiding
or evasion is implemented or recommended. Check the particular game's rules;
close Chromatic if its use is prohibited.

Public Windows API reference:
[MagSetFullscreenColorEffect](https://learn.microsoft.com/en-us/windows/win32/api/magnification/nf-magnification-magsetfullscreencoloreffect).
Hardware-visible behavior, fullscreen presentation and game-specific policy remain
separate manual qualification work. Readback and fake tests cannot establish them.
