# Rust biome starting points

Open Presets / Library and click **Add / update Rust presets**. This saves missing
presets and refreshes exact, unedited earlier Rust defaults while preserving
customized presets and their IDs. It selects the Rust filter. Select a preset,
preview it, then explicitly Apply. Duplicate before customizing if you want to
keep both versions. Reset selected starting point restores just that original.

| Preset | Saturation | Brightness | Contrast | Intended comparison |
| --- | ---: | ---: | ---: | --- |
| Forest / Temperate | 185% | +0.002 | 0.996 | Rich color with a tiny black-level lift |
| Desert / Arid | 170% | −0.003 | 0.99 | Richer color with slightly softer peak whites |
| Snow / Arctic | 160% | −0.010 | 0.98 | More color, neutral black retained and peak whites slightly lowered |
| Tundra | 195% | +0.002 | 0.996 | Strong color with near-neutral contrast |
| Jungle | 180% | +0.003 | 0.994 | Richer greens with a subtle whole-image lift |

The user preferred Siege Strong and found the earlier Rust presets grey while
trying them in Siege; they have not compared them in Rust yet. This revision
raises saturation from 120–145% to 160–195% and lowers the neutral black output
from 0.010–0.070 to 0–0.006. Contrast remains 0.98–0.996, close to neutral.
Siege Strong stays unchanged at saturation 200%, brightness +0.005, contrast 0.98.
These are mathematical differences and a response to appearance feedback, not a
Rust visibility qualification. Compare them on Rust's actual terrain/lighting.

Refreshing is an explicit library action and never applies colors. A renamed,
retagged, recolored or otherwise customized earlier preset is kept. Use Reset
selected starting point to opt into the latest original for one customized entry;
duplicate it first if you want to keep your version. Imported copies stay intact.

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
