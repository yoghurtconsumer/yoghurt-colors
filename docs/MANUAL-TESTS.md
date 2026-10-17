# Manual qualification backlog — Stage 3 library checkpoint

Automated .NET 10 Release/x64 validation: 14 projects build, 229 tests pass.
No physical display, registry, startup, firewall or LAN setting was changed.
The hardware harness was NOT run. Ordinary CI must never run it.

## Safe checks without desktop writes

Double-click Run-Chromatic.cmd. The main WPF window should render even if a
companion fails; diagnostics then explain why Apply is unavailable. Navigate all
pages, edit sliders/sample previews, inspect themes and save/restart settings.
Sliders alone must not change the desktop. Effects start off.

Library checks for this checkpoint (no Apply needed):

1. Set Display saturation to 175%, then save the Display draft as a new named
   Color preset in Library. Restart and verify the saved preset remains.
2. Preview another preset and return to Display: only the local samples/sliders
   should change. Duplicate/rename/tag/filter/delete a copy; reset built-ins and
   check custom presets remain. Per-output settings must stay separate.
3. Generate a CD1 code, paste it into the import box, review the diff, and cancel:
   the library must not gain an entry. Review again and confirm: a new local copy
   appears, with no desktop write. Export/import .chromaticpreset JSON as well.
4. Import a resolution or crosshair document: store/export is available; Apply
   must explain the pending later stage and leave the desktop unchanged.
5. In Games, add/edit/remove an executable/preset bookmark. Nothing launches or
   switches automatically. Remove its chosen preset and check the missing-preset
   message. Optionally click Steam discovery and manually pick an executable.
6. Check readability/keyboard navigation in Default and Slate, especially list
   selection, filters, long paths and share-code text. Actual DPI/accessibility
   qualification is still manual; XAML layout is covered automatically.

Global input, automatic focus switching and Windows protocol handoff are still
pending. Do not interpret saved game bookmarks or a pasted link parser as those
features being implemented.

Game pack checks: click Add game presets, verify four Siege options and the
CS2/VALORANT/Apex game tags. Edit/rename one, add the pack again and verify the
edits remain and no duplicate IDs are created. Reset selected starting point
must restore only that preset and leave the Display draft/desktop unchanged.
Adding the pack must not apply colors or change game files/settings.

Optional user-run visual comparison: on SDR, start with Siege Balanced and use
explicit Apply. Compare Neutral, Indoor lift and Outdoor balance on the maps and
lighting you actually play. Observe shadow detail, black level, bright windows,
colored outlines and whether strong saturation hides detail through clipping.
Indoor lift changes the whole image. Record the chosen settings, monitor and
backend result; mathematical gray-range tests do not prove visibility benefits.
No game or physical display is used for automated preset tests.

Run-Chromatic.cmd -SmokeTest verifies first render and closes with in-memory
defaults, without launching companions or native resource providers. Logs are at
%LOCALAPPDATA%/ChromaticDesktop/logs/latest-launch.log and startup reports.
Automated launcher tests cover spaces/foreign working directory and visible errors.

The built app's --diagnostics switch writes read-only-diagnostics.json beside the
launcher log without companions or resource initialization. On newer Windows it
records ColorInfo2 active SDR/WCG/HDR separately from legacy flags. The local
BenQ display reported legacy flags 5 but modern flags 69 and active SDR; this
regression is fixed and tested. Windows Settings showed HDR unsupported and
automatic color management off. No setting was changed while inspecting them.

After restarting the updated app on SDR, move saturation to a visibly different
value, press Apply and inspect its nearby result. A human must verify visible
color change and captured-baseline restoration. If blocked, the reason must be
shown beside Apply; connection/optional-tone failures must not appear successful.

For a human fullscreen NVIDIA check, opt in to experimental private NVIDIA DVC
in Settings, Save and restart. Prepare NVIDIA vibrance, select one output and
explicitly Apply. Confirm the backend result names Magnification+NvidiaDvc and
check the visible game, Alt+Tab back and baseline return on Restore/Exit. Software
fallback is explicit and does not establish exclusive-fullscreen support. Changing
Saturation away from 100% selects software again; use the separate Vibrance scale.
Do not automate the game, alter its files or bypass its protection/policies.

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
