# Rust biome starting points

Open Presets / Library and click **Add Rust biome presets**. This saves missing
presets, selects the Rust filter and preserves existing edits. Select a preset,
preview it, then explicitly Apply. Duplicate before customizing if you want to
keep both versions. Reset selected starting point restores just that original.

| Preset | Saturation | Brightness | Contrast | Intended comparison |
| --- | ---: | ---: | ---: | --- |
| Forest / Temperate | 135% | +0.015 | 0.96 | Moderate color and a small whole-image lift |
| Desert / Arid | 120% | −0.010 | 0.96 | Restrained color and softer sunlit neutral tones |
| Snow / Arctic | 125% | −0.015 | 0.94 | Lower peak whites with a small neutral black-level lift |
| Tundra | 145% | +0.005 | 0.98 | More color separation to compare on muted terrain |
| Jungle | 125% | +0.030 | 0.92 | Larger whole-image lift with restrained saturation |

These values are original Chromatic appearance choices, not Facepunch settings
or measured visibility improvements. They use the existing global affine SDR
matrix. Gamma remains 1; warmth, shadows, highlights, vibrance and CVD remain
neutral. Optional gamma/NVIDIA services are not required by these presets.

Mathematical tests verify neutral gray steps remain distinct and within range,
alpha is preserved, Jungle lifts dark neutrals more than Forest, and Snow/Desert
lower peak neutral whites. Saturated colored pixels can still clip; raising black
levels can wash out contrast. Actual results depend on monitor, lighting, game
settings and presentation path. Compare against Neutral on the maps you play.

The whole screen changes, including terrain and vegetation. The utility does not
detect the current biome or enemies, remove foliage, or recover information absent
from the game's rendered image. Selection and application remain manual. These
presets add no game-memory/file access, rendering hooks or game input.

Biome names follow the five entries in the
[Facepunch-hosted Terrain wiki](https://wiki.facepunch.com/rust/Terrain), checked
17 October 2026. Facepunch's [Jungle Update](https://rust.facepunch.com/news/jungle-update)
also documents Jungle. Those sources establish environment names only, not the
effectiveness of these independently chosen color settings. Custom server maps
and mixed lighting may need a different preset or your own adjustments.
