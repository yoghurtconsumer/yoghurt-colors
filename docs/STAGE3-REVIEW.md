# Stage 3 review

All new listeners start off. Development validation used injected input/display
adapters; no hooks were installed and no desktop, registry, startup, firewall or
LAN setting was changed by automated tests. Live input/GPU qualification remains
manual. Exit an older Chromatic instance using Exit before running this build.

## Automatic game profiles

1. In Library, add game presets if needed. Preview your preferred look first.
2. In Games, Browse executable, name the bookmark, choose its Color preset and
   Save bookmark. Select the saved entry and Draft automatic rule for selected.
3. Check the new rule, check Enable automatic foreground profiles and Save
   automation configuration. Read the runtime status below the button.
4. Set your preferred desktop colors on Display, return to Games and Use Display
   base draft as desktop fallback, then Save automation configuration.
5. Explicitly Apply from Display to enable effects. Switch to the selected game:
   after the 150 ms debounce, the saved game look should become active. Moving to
   Chromatic, desktop, an unmatched/denied process or an exited game uses desktop
   fallback. Global Magnification changes every display.
6. Uncheck/remove a rule and Save to disable/remove that automatic binding. Editing
   or deleting a bookmark alone does not change its separately saved rule. After
   editing a library preset or executable path, draft its rule again and Save
   automation to update the saved snapshot. A removed library preset must be
   replaced or its rule/hotkey removed before saving.

Only process path, PID and creation metadata are queried. No executable contents,
game memory, input automation, game launch or game-file change is involved.
Protected processes can deny metadata; do not elevate or disable protections.

## Temporary hold and hotkeys

Use an enabled exact-path game rule from the previous steps. For hold, enable the
hold listener, choose a chord (default F6 is merely a suggestion), set strength
and optional tint, then Save. Mouse4/Mouse5 additionally require the separate
mouse-listener checkbox. No binding is assumed free. While the matching game is
foreground and effects have successfully applied, press and hold: a basic
whole-image affine lift should appear. Any chord component release restores the
newest base look, including preset edits made while held. It is not selective
shadow correction. Gamma/tonal LUT/native-DVC hold remains unavailable until
latency qualification. Do not infer performance from fake tests.

Test repeat, a released modifier, Alt-Tab while held, lock/unlock, sleep/resume,
Pause, Restore and disconnect. No hold should resume without a fresh press.
OS safety suspensions may require explicit Apply again. Reconciliation checks
configured keys every 100 ms and may release a missed key-up; it cannot turn a
hold on. Injected events are ignored by default; accessibility input is an
explicit option. Low-level callbacks run in Chromatic and always pass input on.

For a discrete hotkey, choose action and a Ctrl/Alt/Shift chord plus one key,
choose a Color preset if applicable, Draft hotkey, enable hotkeys and Save.
Registered/conflicting IDs and Windows error codes appear in runtime status.
Preset changes do not turn disabled effects on; Pause toggles pause; Restore
disables effects. Screenshot dispatch reports NotImplemented until Stage 5.

## Preset links

Pasting CD1 codes and `.chromaticpreset` import always remain available. To enable
OS links, use Settings > Register preset links from a stable app folder. This
explicit click writes only your `HKCU\Software\Classes\chromaticdesk` handler.
Remove my registration removes it only if the fixed quoted command still belongs
to this app. Another location/app is never overwritten automatically.

Open `chromaticdesk://preset/<CD1-code>`: it should bring the same running app to
Library with an import diff. Cancel leaves the library unchanged; Confirm import
saves a fresh local ID. Applying is a separate action. Resolution/full setup and
crosshair application remain pending Stages 4/5. Oversized codes may exceed
Windows/browser URL limits; paste the offline code or use a file instead.

Double-launch should show one UI and one Agent/Recovery
pair. Local handoff pins the real OS peer, exact executable image, user/session
and creation time, and uses a fresh challenge plus bounded strict JSON. It has no
native/effects command surface. The endpoint is fixed per product/user/session, including across app folders.
A different app location fails exact-image authentication and must not start a
second UI/Agent; exit the old build before switching locations. Automated pipe
tests inject a unique scope and never connect to the real app endpoint.

## Rust colour-darkening review

The supplied [YouTube mirror](https://www.youtube.com/watch?v=-xwbPDYUWp8)
plays and was visually inspected. At about 10 seconds it shows PlexusX's Hue turn
and Darken a colour controls, including 400% colour depth; later frames show the
Rust Accessibility colour menu. The original TikTok remained inaccessible. No
challenge, account or age data was submitted. The exact reference formula and
its claimed game/anti-cheat compatibility have not been independently verified.

Display now contains **Experimental colour darkening · Rust accent tests**.
Use **Prepare blue holo test** to preview an original global transform, choose
Rust's blue holo colour yourself, then use the separate **Apply colors to desktop**.
The **Prepare blue-tinted loot test** is experimental: neutral white stays white,
so it is not a verified black loot solution. Other matching colours change too.
No game-element identification, game capture, overlay, file edits or input
synthesis is used. Accepted Siege/Rust presets remain unchanged.

Follow [the Rust review steps](RUST-COLOUR-DARKENING.md), including Restore,
checking unrelated colours and saving/sharing the draft. No live Rust/hardware
qualification was run by development. Stage 3 implementation is complete;
Stage 4 was not started. Follow game policy: the architecture does not establish
anti-cheat approval or a guarantee against false flags. See ANTI-CHEAT-BOUNDARY.md.
