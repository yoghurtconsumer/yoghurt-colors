# Compatibility evidence — Stage 2

| Area | Actual evidence |
| --- | --- |
| .NET 10 Windows x64 build | SDK 10.0.402; 14 Release/x64 projects; zero warnings/errors |
| Core math/reducer/planner/ranges | 58 passing Core tests |
| Storage/schema/migration/ownership/ABI/IPC/native fakes | Included in 113 passing integration tests |
| Independent process recovery | Six passing scenarios: normal shutdown, crash after setter, hung setter, UI death, foreign writer, UI Apply-to-Agent/readback/restore; file-backed resources only |
| Safety through real named pipes | Verified SID/session/PID/start-time/nonce; restricted Recovery role; Disable fences a pending journal; correlated responses |
| WPF/DI/themes/preview/Apply results | Included in 15 passing UI/launcher tests; blocked/failed/superseded Apply, progress, stable selection, optional-tone reset and draft-only NVIDIA preparation |
| Actual .cmd and rendered window | Regression tests cover spaces, unrelated working directory, older SDK precedence, unavailable pinned SDK with full stderr, missing executable and clean smoke exit |
| Windows 10 19041 / 22H2 / other Windows 11 builds | Target and runtime gates present; OS-by-OS qualification untested |
| Magnification visible output on AMD/Intel/NVIDIA | Native implementation present; physical hardware NOT tested |
| Private NVIDIA DVC/hybrid laptop/driver versions | Fake mapping/range/version tests only; experimental, unqualified |
| SDR gamma / calibration / driver reset | Fake baseline/readback/slow-write tests only; optional, unqualified |
| Multiple active gamma outputs | Blocked: shared driver ramps are unqualified |
| HDR/ACM, hotplug, mixed DPI/negative coordinates | Safety refuses unknown/advanced color and suspends on events; actual transitions untested |
| Local read-only color detection | BenQ XL2540X+, build 26200: legacy flags=5 falsely implied advanced color; fixed modern flags=69/active SDR correctly permits SDR. No setter executed |
| Modes/stretch/custom timings/DDC | NotImplemented; Stage 4 |
| Siege Fullscreen saturation | User reports successful play on 16 October with Saturation up to 300%, confirmed on 17 October; exact presentation path/backend acknowledgment not captured |
| Exclusive fullscreen / capture / anti-cheat | No blanket compatibility claim; the user report does not identify exclusive scanout, capture parity or anti-cheat approval |
| Display-notification preservation | Actual handler with injected backend: repeated unchanged SDR mappings preserve confirmed matrix/DVC; unsafe or unconfirmed/tonal cases restore; zero reapply writes |
| Fullscreen NVIDIA candidate | Injected private API plan, native/software result distinction, no double vibrance and exact restoration tested; physical fullscreen behavior unqualified |
| Phone/browser/Private LAN / GSMTC | NotImplemented; no listener/transport |

The current test machine is Windows build 26200. Automated process tests use only
injected resources; they do not qualify GPU behavior. The manual hardware harness
was compiled but NOT run. No driver version is listed as supported.
Windows Settings inspection on 16 October showed HDR unsupported and automatic
color management off. No Windows setting was changed. This validates the probe
correction only, not physical saturation or fullscreen/capture behavior.
The user initially reported desktop/Borderless operation but lost color in
Fullscreen Siege with no Alt+Tab recovery. On 17 October they subsequently reported
successful Fullscreen play on 16 October using the Saturation slider up to 300%,
not the separate NVIDIA vibrance control. This corrects a blanket assumption that
desktop saturation cannot affect the game's Fullscreen mode. Both observations
are user-reported; exact build/settings and exclusive/flip presentation mode were
not captured. Read-only GPU inventory: RTX 3060, driver
32.0.16.1692. No game interaction or physical driver setter was performed here.

Private testing ZIP: App/Agent/Recovery are self-contained .NET 10.0.13 win-x64.
Each startup smoke verified its bundled CoreLib with SDK/runtime paths removed;
App rendered/closed without providers. ZIP entries are SHA-256 checked. No clean
Windows VM, hardware or game qualification is implied. Latest whole-solution
Debug/x64 validation: zero warnings/errors and 186 tests; Release rebuild was
blocked by the user's running Release app and was not forced closed.
