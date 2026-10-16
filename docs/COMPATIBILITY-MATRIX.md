# Compatibility evidence — Stage 2

| Area | Actual evidence |
| --- | --- |
| .NET 10 Windows x64 build | SDK 10.0.402; 14 Release/x64 projects; zero warnings/errors |
| Core math/reducer/planner/ranges | 58 passing Core tests |
| Storage/schema/migration/ownership/ABI/IPC/native fakes | Included in 94 passing integration tests |
| Independent process recovery | Six passing scenarios: normal shutdown, crash after setter, hung setter, UI death, foreign writer, UI Apply-to-Agent/readback/restore; file-backed resources only |
| Safety through real named pipes | Verified SID/session/PID/start-time/nonce; restricted Recovery role; Disable fences a pending journal; correlated responses |
| WPF/DI/themes/preview/Apply results | Included in 14 passing UI/launcher tests; blocked/failed/superseded Apply, progress, stable selection and optional-tone reset |
| Actual .cmd and rendered window | Regression tests cover spaces, unrelated working directory, older SDK precedence, unavailable pinned SDK with full stderr, missing executable and clean smoke exit |
| Windows 10 19041 / 22H2 / other Windows 11 builds | Target and runtime gates present; OS-by-OS qualification untested |
| Magnification visible output on AMD/Intel/NVIDIA | Native implementation present; physical hardware NOT tested |
| Private NVIDIA DVC/hybrid laptop/driver versions | Fake mapping/range/version tests only; experimental, unqualified |
| SDR gamma / calibration / driver reset | Fake baseline/readback/slow-write tests only; optional, unqualified |
| Multiple active gamma outputs | Blocked: shared driver ramps are unqualified |
| HDR/ACM, hotplug, mixed DPI/negative coordinates | Safety refuses unknown/advanced color and suspends on events; actual transitions untested |
| Local read-only color detection | BenQ XL2540X+, build 26200: legacy flags=5 falsely implied advanced color; fixed modern flags=69/active SDR correctly permits SDR. No setter executed |
| Modes/stretch/custom timings/DDC | NotImplemented; Stage 4 |
| Exclusive fullscreen / capture / anti-cheat | No compatibility claim |
| Phone/browser/Private LAN / GSMTC | NotImplemented; no listener/transport |

The current test machine is Windows build 26200. Automated process tests use only
injected resources; they do not qualify GPU behavior. The manual hardware harness
was compiled but NOT run. No driver version is listed as supported.
Windows Settings inspection on 16 October showed HDR unsupported and automatic
color management off. No Windows setting was changed. This validates the probe
correction only, not physical saturation or fullscreen/capture behavior.
