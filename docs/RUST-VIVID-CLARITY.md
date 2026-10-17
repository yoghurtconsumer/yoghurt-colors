# Rust — Vivid clarity

Original starting look requested from the user's unfiltered Rust training-server
screenshot on 17 October 2026. This has not been tuned or verified on their
physical display. It is not a promise of perfect visuals or improved spotting.

The import works in the current portable app; no update or SDK is needed:

1. Use Restore if the old holo experiment is still active.
2. Presets / Library > Import JSON file > select
   `docs/presets/Rust-Vivid-Clarity.chromaticpreset`.
3. Review the diff, Confirm import, then Preview selected.
4. Explicitly Apply selected colors to desktop. Compare shaded rocks, grass, sky,
   clothing and bright clouds, then Restore to compare the original.

Alternatively paste the code in `docs/presets/Rust-Vivid-Clarity.cd1.txt` into the
import box, Review pasted import and Confirm. The new ID is a local copy. Import
and Preview never apply automatically. Existing accepted presets stay intact.

Saturation 185%, brightness +0.0125, contrast 0.975. Hue rotation / darkening /
warmth / vibrance / CVD / shadows / highlights are off and gamma is neutral.
Optional gamma or NVIDIA services are not required. These values mildly lift
all tones: a neutral 5% input becomes 7.375%, black becomes 2.5%, white remains
100%. Gray steps remain distinct. The black lift is a tradeoff against deep
blacks; lower Brightness if the picture looks washed out. Saturated pixels can
still clip; lower Saturation if grass or sky loses texture.

This changes the whole desktop. It does not remove shadows cast by rocks or
recover fully black / occluded detail. Shadow rendering is controlled by Rust's
own settings; Chromatic does not change those settings, files or game memory.
Compare this look in the actual lighting you play and adjust manually. There is
no reticle/loot/biome/enemy detection or object-only treatment.
