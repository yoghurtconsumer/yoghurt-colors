# Manual qualification backlog — Stage 2

Automated .NET 10 Release/x64 validation: 14 projects build, 140 tests pass.
No physical display, registry, startup, firewall or LAN setting was changed.
The hardware harness was NOT run. Ordinary CI must never run it.

## Safe checks without desktop writes

Double-click Run-Chromatic.cmd. The main WPF window should render even if a
companion fails; diagnostics then explain why Apply is unavailable. Navigate all
pages, edit sliders/sample previews, inspect themes and save/restart settings.
Sliders alone must not change the desktop. Effects start off.

Run-Chromatic.cmd -SmokeTest verifies first render and closes with in-memory
defaults, without launching companions or native resource providers. Logs are at
%LOCALAPPDATA%/ChromaticDesktop/logs/latest-launch.log and startup reports.
Automated launcher tests cover spaces/foreign working directory and visible errors.

Check keyboard navigation, high contrast, screen reader labels, mixed DPI, tray
Show/Pause/Restore/Exit and actual close-to-tray behavior manually. Those checks
have not been claimed as complete visual/accessibility qualification.

## Explicit hardware qualification only

Close the normal app first. After reviewing the source and arranging a safe
desktop, a human may build the solution and launch:

    tests/Chromatic.HardwareHarness/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/Chromatic.HardwareHarness.exe --hardware --acknowledge-display-changes

The harness also requires an interactive APPLY acknowledgment, counts down five
seconds, applies only 110% global saturation for ten seconds, then requests exact
baseline restoration. Ctrl+C requests restoration. Independent Recovery observes
the Agent. Missing flags/redirected stdin perform no test. No automatic mode/HDR,
registry, DDC or firewall changes are part of this harness.

Record OS/build, GPU/driver, physical displays/topology, initial calibration/filter,
requested route, API readback, visible result, clean exit and baseline return.
Verify normal exit, UI termination, Agent termination/hang, session lock/suspend
and externally changed color values on controlled hardware. A foreign value must
remain untouched and require local review. Do not claim GPU support from fakes.

Manually qualify HDR/ACM refusal and mixed SDR/HDR suspension; existing Magnifier/
Night Light/calibration conflicts; NVIDIA missing/private-version/invalidated/
hybrid output cases; software/native switching without double vibrance; gamma
monotonic grayscale and ignored ramps. Gamma writes over 250 ms disable the
session's route. Live hold remains unavailable without latency qualification and
Stage 3 input. Multiple gamma outputs are blocked pending safe shared-ramp
qualification. Visible output and recording parity need separate checks.

Later Stages 3–7 own input/game/import checks, full mode/HDR/DDC transactions,
overlay/capture, phone/GSMTC and packaged/signing/licensing qualification.
