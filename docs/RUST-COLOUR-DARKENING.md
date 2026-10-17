# Experimental Rust colour-darkening review

This is an original desktop-wide colour transform, inspired by the separate
Hue turn / Darken a colour controls visible at about 10 seconds in the user's
[PlexusX reference](https://www.youtube.com/watch?v=-xwbPDYUWp8). The exact PlexusX
formula is unknown; this is not a claimed replica. No game or physical display
was used to tune or qualify these test drafts. Existing Siege and biome presets
are unchanged.

## Try the holo draft

1. Exit the older Chromatic using Exit, then open the new build on SDR.
2. On Display, expand **Experimental colour darkening · Rust accent tests**.
3. Click **Prepare blue holo test**. This changes only the draft/sample preview:
   saturation 150%, hue rotation +15 degrees, target hue 255 degrees, darkening
   400%. It resets other global colour controls; it does not apply anything.
4. In Rust, choose **Blue** for Holo Sight Colour in the Accessibility colour
   settings yourself. Chromatic does not change Rust settings or files.
   Facepunch's [accessibility change history](https://commits.facepunch.com/r/rust_reboot/main/hackweek_accessibility_colourblind)
   records the green/blue holo choices; black is not assumed to be a native choice.
5. Return to Chromatic, inspect the Blue sight / White / Black sample swatches,
   then explicitly click **Apply colors to desktop**. Read the actual result.
6. Compare the sight in Rust, then inspect blue terrain, sky, text, inventory,
   clothing and other apps. All matching colours change. Adjust the target hue
   and strength if desired, and Apply again. No improved visibility is promised.
7. Use Restore to request the captured original desktop matrix, or reset the
   colour-darkening draft and Apply. Resetting a draft alone does not write the
   display. The existing Exit/recovery ownership protections remain in use.

The 400% darkening scale is separate from Saturation, which still stops at 300%.
Hue rotation is -180 to +180 degrees. Target hue uses 0 red, 120 green, 240 blue,
360 red. A target of 255 follows the rotated-blue direction in this test draft.

## Loot-highlight test and current limit

**Prepare blue-tinted loot test** sets saturation 120%, no hue rotation, target
240 degrees and strength 400%. This tests whether the visible accent has a blue
component. It does not identify loot, hover state or inventory. Neutral white
stays white: a truly neutral white loot highlight will not become black. A black
loot result has NOT been verified and remains an open user requirement.

Do not infer success from the black Blue sight sample alone. Check the actual
hover accent in Rust and report which other colours change. A global transform
cannot treat identical input colours differently by game-object identity. No
capture/recognition, game-memory access, injection, DirectX hook, game-file edit,
driver or automated input is used. See [the boundary review](ANTI-CHEAT-BOUNDARY.md);
there is no anti-cheat approval or zero-false-flag guarantee.

## Saving and implementation

Save the Display base draft in Library to retain these values; CD1/file imports
show the three fields in the diff before confirmation. Preview, prepare, save,
share and import do not apply. Automatic rules keep saved colour snapshots, so
redraft and save a rule after editing its library preset. Temporary hold lift is
never saved as the base profile.

The pure Core implementation rotates RGB about the neutral axis and subtracts
the selected chromatic projection equally from all RGB channels. The neutral
axis has zero projection: black, gray and white remain unchanged by these two
operations. Opposite hues brighten, signed coefficients can exceed gamut and
final display clipping can hide coloured detail. This is an affine encoded-RGB
approximation rather than perceptually uniform hue editing or HDR processing.
It composes with the existing global saturation/CVD/contrast/warmth matrix.
Gamma and selected-output warmth remain separate per-monitor intentions.

All three added fields default to zero and are omitted at zero. Local v1 schemas
accept bounded values; old documents load without a migration. Older app builds
reject documents carrying the new fields. The remote ten-control whitelist is
unchanged. Fake-only tests cover the math, ranges, draft/Apply, storage/share,
IPC, remote rejection and exact captured-baseline restoration. Hardware-visible
behaviour, fullscreen presentation and game policy require separate review.
