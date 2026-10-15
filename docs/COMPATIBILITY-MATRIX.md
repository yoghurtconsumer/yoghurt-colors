# Compatibility evidence — Stage 2

| Area | Actual evidence |
| --- | --- |
| .NET 10 Windows x64 build | SDK 10.0.402; 14 Release/x64 projects; zero warnings/errors |
| Core math/reducer/planner/ranges | 52 passing Core tests |
| Storage/schema/migration/ownership/ABI/IPC/native fakes | Included in 81 passing integration tests |
| Independent process recovery | Five passing scenarios: normal shutdown, crash after setter, hung setter, UI death, foreign writer; file-backed resources only |
| Safety through real named pipes | Verified SID/session/PID/start-time/nonce; restricted Recovery role; Disable fences a pending journal; correlated responses |
| WPF/DI/themes/preview/Apply results | Included in 7 passing UI/launcher tests |
| Actual .cmd and rendered window | Regression tests cover spaces, unrelated working directory, older SDK precedence, missing executable and clean smoke exit |
| Windows 10 19041 / 22H2 / other Windows 11 builds | Target and runtime gates present; OS-by-OS qualification untested |
| Magnification visible output on AMD/Intel/NVIDIA | Native implementation present; physical hardware NOT tested |
| Private NVIDIA DVC/hybrid laptop/driver versions | Fake mapping/range/version tests only; experimental, unqualified |
| SDR gamma / calibration / driver reset | Fake baseline/readback/slow-write tests only; optional, unqualified |
| Multiple active gamma outputs | Blocked: shared driver ramps are unqualified |
| HDR/ACM, hotplug, mixed DPI/negative coordinates | Safety refuses unknown/advanced color and suspends on events; actual transitions untested |
| Modes/stretch/custom timings/DDC | NotImplemented; Stage 4 |
| Exclusive fullscreen / capture / anti-cheat | No compatibility claim |
| Phone/browser/Private LAN / GSMTC | NotImplemented; no listener/transport |

The current test machine is Windows build 26200. Automated process tests use only
injected resources; they do not qualify GPU behavior. The manual hardware harness
was compiled but NOT run. No driver version is listed as supported.
