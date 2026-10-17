# Rust holo / loot request: unsupported element isolation

The user tested the previous 400% blue-holo draft and reported that the whole
screen became dark gray / black and white. They want only the actual holo reticle
and hovered loot accent changed. The global matrix cannot deliver that isolation.
The two misleading Prepare holo/loot test shortcuts have been removed.

## Reset an older draft

Use **Restore** to request the captured original desktop colours. On Display,
expand **Advanced global colour darkening**, press **Reset colour darkening / hue
rotation**, choose your usual look and explicitly Apply. Resetting a draft alone
does not write the display. Existing accepted Siege / Rust biome presets are
unchanged. Old imported/saved drafts retain their values; they are never silently
rewritten or automatically applied on startup.

## Scope of the remaining controls

These are advanced global colour operations: rotation -180 to +180 degrees,
target hue 0 red / 120 green / 240 blue / 360 red and strength 0–400% (zero off).
The original affine formula subtracts a chromatic projection equally from RGB.
It changes matching colours everywhere and can brighten opposite hues. Signed
out-of-gamut values are clipped only at final output; high strength can crush
coloured detail. Neutral white stays white. The preceding 400% test was excessive
for the user's intended task and is no longer provided as a recommended draft.

A pixel-colour transform does not identify game objects or hover state. Identical
input colours receive identical output colours regardless of whether they belong
to a sight, loot highlight, sky, terrain, text or another application. Object-only
recolouring would need a game-native option or a supported integration that
exposes those elements. No such integration is implemented or claimed.

The [user's reference video](https://www.youtube.com/watch?v=-xwbPDYUWp8) was
visually inspected: PlexusX's Hue turn / Darken a colour controls were visible,
but their exact algorithm and claimed game-specific result were not independently
verified. The reference does not prove that Chromatic can isolate those elements.
No game capture/recognition, memory access, injection, DirectX hook, file edit,
driver or input synthesis has been added. See [the boundary review](ANTI-CHEAT-BOUNDARY.md).

The three local fields still save/share/import as bounded optional v1 fields,
omitted at zero; remote controls remain unchanged. Keeping these generic controls
does not establish a working black holo or loot feature. Those user requirements
remain unsupported by this backend. Stage 4 Resolution work is independent.
