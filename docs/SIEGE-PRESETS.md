# Siege named styles

Open Presets / Library, click **Add game presets**, then search for Champ or
Im Diamond 1. The Rainbow Six Siege game tag lists all six Siege choices. Preview
locally, then explicitly Apply. Adding the pack preserves your edited presets;
duplicate before customizing if you want both versions. Selected reset restores
only the chosen bundled starting point, without applying it.

| Style | Saturation | Brightness | Contrast | Intended comparison |
| --- | ---: | ---: | ---: | --- |
| Champ | 220% | +0.002 | 0.996 | Bold color with near-neutral contrast and a tiny black-level lift |
| Im Diamond 1 | 190% | +0.005 | 0.99 | Less intense color than Champ, with a little more whole-image lift |
| Strong color (unchanged) | 200% | +0.005 | 0.98 | The user's preferred existing comparison point |

These are original Chromatic styles named at the user's request. The names do
not imply rank qualification, a professional player's settings, or measured
visibility/performance improvements. No game or physical display was used during
development or automated testing. In-game comparison remains required.

Both new styles keep gamma at 1 and warmth/shadows/highlights/vibrance/CVD neutral.
They use the existing global affine SDR route; optional LUT/NVIDIA controls are
not required by the presets. Automated tests check distinct neutral gray steps,
alpha/ranges, Champ's small black-level lift, portable sharing and library updates
that preserve edits and never send an Apply command on add or preview.

Compare dark transitions, clothing, terrain and bright outlines on the maps you
play. Strong saturation can clip colored pixels or reduce texture detail even
when neutral grays remain in range. Lower Saturation in Display if needed, then
save a duplicate. These settings change the whole screen and do not detect enemies
or selectively alter game objects.
