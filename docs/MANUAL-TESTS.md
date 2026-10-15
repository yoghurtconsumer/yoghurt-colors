# Manual qualification backlog

No hardware tests were run for Stage 1. SDK 10.0.402 is installed; the normal
solution build and all 95 tests pass. Actual app startup/default close passed on
Windows build 26200. Use Run-Chromatic.cmd for the following interactive checks.

Stage 1 UI checks: launch on Windows x64, navigate every page using keyboard,
inspect capability/status text, change Default/Slate, save settings, restart,
check default close exits, opt in to tray, Show/Pause/Restore/Exit, and check
high contrast/scaling/screen reader behavior. These are not pixel tests.

Launcher regression: `Run-Chromatic.cmd -SmokeTest` builds the real x64 app,
waits for a visible first-render acknowledgment, then closes with in-memory
default settings. Run without flags to keep the window open. On error the console
must stay open and point to LOCALAPPDATA/ChromaticDesktop/logs/latest-launch.log.
The automated suite also covers spaces in paths and an unrelated working directory.
The Display controls affect sample swatches only; desktop color writes remain off.

Later stages require explicit user opt-in with independent rollback before any
display write: captured baseline restoration, crash/UI death, owned resource
conflicts, lock/suspend/disconnect, hotplug, mixed DPI/negative coordinates,
AMD/Intel/NVIDIA SDR, HDR/ACM, 59.94/60/high refresh, preview timeout/Keep/Revert,
DDC Undo, overlay input/focus, capture routing, phone teardown and real GSMTC.
Do not run the hardware harness in ordinary CI. Stage 1 harness exits with code 2.
