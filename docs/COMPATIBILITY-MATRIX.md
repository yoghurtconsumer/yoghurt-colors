# Compatibility evidence

| Area | Status at Stage 1 |
| --- | --- |
| .NET 10 Windows x64 solution build | Passed: SDK 10.0.402, Release/x64; 13 projects, 0 warnings/errors |
| Reducer, schemas, storage, migrations, fake coordinator and color math | Passed: 46 Core + 44 integration tests on .NET 10 / VSTest |
| WPF XAML, DI, themes and sample preview | Passed: 3 WPF shell tests on .NET 10 |
| Actual .cmd launcher, paths with spaces/foreign working directory and failure reporting | Passed: 2 process regression tests; first-render acknowledgment and clean exit |
| Real x64 app startup/default close | Passed on Windows build 26200; main window created; exit code 0 |
| Windows 10 19041 / 22H2 and other Windows 11 builds | Runtime guards present; release-by-release qualification untested |
| AMD / Intel / NVIDIA SDR | Untested; no native APIs enabled |
| Multi-monitor / negative coordinates / mixed DPI | Untested; identities/scope contracts only |
| HDR / ACM transitions | Untested; planner refuses SDR with advanced color |
| DDC/CI, 59.94/60/high refresh, stretch, custom modes | Untested; typed intentions only |
| Exclusive fullscreen / anti-cheat / capture parity | No compatibility claim |
| Phone browsers / Private LAN / firewall | Untested; no listener |
| GSMTC real sessions | Untested; no transport |

Passing fake tests establishes foundation invariants, not GPU or monitor support.
