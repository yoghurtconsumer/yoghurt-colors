using System.Collections.Immutable;

namespace Chromatic.Core.Models;

public sealed record GamePresetStartingPoint(PresetDocument Preset, string Description);

// Original, uncalibrated appearance choices. No game-state input is used.
// Keep LUT-only controls neutral so the pack uses the global affine SDR route.
public static class GamePresetCatalog
{
    public const string Siege = "Rainbow Six Siege";
    public const string Rust = "Rust";
    public static ImmutableArray<GamePresetStartingPoint> Entries { get; } =
    [
        Make("siege-balanced", "Siege — Balanced", Siege, new(150, Brightness: .01, Contrast: .98),
            "A moderate color boost with a small brightness and contrast adjustment. Start here and compare with Neutral."),
        Make("siege-indoor", "Siege — Indoor lift", Siege, new(135, Brightness: .035, Contrast: .92),
            "Raises dark tones by lifting the whole image and reducing contrast slightly. Blacks look lighter; this is not a shadow-only effect."),
        Make("siege-outdoor", "Siege — Outdoor balance", Siege, new(145, Brightness: -.005, Contrast: .98),
            "Slightly lowers overall brightness with moderate color separation. Compare bright windows and outdoor areas before choosing it."),
        Make("siege-color", "Siege — Strong color", Siege, new(200, Brightness: .005, Contrast: .98),
            "Stronger color for your own comparison. Saturated colors can clip and lose detail; reduce Saturation if that happens."),
        Make("siege-champ", "Siege — Champ", Siege, new(220, Brightness: .002, Contrast: .996),
            "Bold color above Strong color, with near-neutral contrast and a tiny black-level lift. Compare dark transitions, clothing and bright outlines. Reduce Saturation if colors lose texture detail."),
        Make("siege-diamond1", "Siege — Im Diamond 1", Siege, new(190, Brightness: .005, Contrast: .99),
            "Rich color with a little more whole-image lift and less saturation than Champ. Compare shaded rooms and bright windows. A gentler starting point if Champ feels too intense."),
        Make("cs2-balanced", "CS2 — Balanced", "Counter-Strike 2", new(155, Brightness: .01, Contrast: .98),
            "A color-forward starting point with a small overall lift. Compare surfaces, outlines and bright areas on the maps you play."),
        Make("valorant-balanced", "VALORANT — Balanced", "VALORANT", new(135),
            "A restrained color boost. Check your chosen in-game outline color and lower saturation if outlines lose detail."),
        Make("apex-balanced", "Apex — Balanced", "Apex Legends", new(145, Brightness: .008, Contrast: .98),
            "A moderate color boost and small overall lift. Compare indoor areas, sky and terrain under the lighting you play."),
        Make("rust-forest", "Rust — Forest / Temperate", Rust, new(185, Brightness: .002, Contrast: .996),
            "Rich color with near-neutral contrast and only a tiny black-level lift. Compare tree shade, foliage and clothing detail. Vegetation changes color too."),
        Make("rust-desert", "Rust — Desert / Arid", Rust, new(170, Brightness: -.003, Contrast: .99),
            "Richer color with slightly softer peak whites and a tiny black-level lift. Compare sand, rocks and shaded objects without a heavy whole-image lift."),
        Make("rust-snow", "Rust — Snow / Arctic", Rust, new(160, Brightness: -.01, Contrast: .98),
            "More color while keeping neutral black at black and lowering peak whites slightly. Compare snow glare and shaded silhouettes; it cannot recover missing rendered detail."),
        Make("rust-tundra", "Rust — Tundra", Rust, new(195, Brightness: .002, Contrast: .996),
            "Strong color for muted ground and mixed vegetation, with near-neutral contrast and deep blacks. Compare clothing and terrain detail; reduce Saturation if colors clip."),
        Make("rust-jungle", "Rust — Jungle", Rust, new(180, Brightness: .003, Contrast: .994),
            "Richer greens with a subtle whole-image lift and near-neutral contrast. Compare shade and foliage without the earlier raised-black look. It does not reveal obscured objects.")
    ];
    public static ImmutableArray<PresetDocument> Presets => Entries.Select(e => e.Preset).ToImmutableArray();
    public static PresetDocument? FindOriginal(string id) =>
        Entries.Select(e => e.Preset).Concat(BuiltInPresets.Create()).FirstOrDefault(p => p.Id == id);
    // Upgrade only the exact first Rust defaults. User edits, imported copies,
    // metadata and bookmark IDs remain intact; nothing is applied by this helper.
    public static PresetDocument RefreshUneditedRustPreset(PresetDocument preset)
    {
        ColorSettings? previousColor = preset.Id switch
        {
            "builtin-game-rust-forest" => new(135, Brightness: .015, Contrast: .96),
            "builtin-game-rust-desert" => new(120, Brightness: -.01, Contrast: .96),
            "builtin-game-rust-snow" => new(125, Brightness: -.015, Contrast: .94),
            "builtin-game-rust-tundra" => new(145, Brightness: .005, Contrast: .98),
            "builtin-game-rust-jungle" => new(125, Brightness: .03, Contrast: .92),
            _ => null
        };
        if (previousColor is null || FindOriginal(preset.Id) is not { } original || preset.Assets.IsDefault ||
            !preset.Assets.SequenceEqual(original.Assets)) return preset;
        var previous = original with { Color = previousColor };
        return (preset with { Assets = previous.Assets }) == previous ? original : preset;
    }
    public static string Describe(PresetDocument? preset)
    {
        if (preset is null) return "Select a preset to see its purpose.";
        var entry = Entries.FirstOrDefault(e => e.Preset.Id == preset.Id);
        if (entry is null) return preset.Kind == PresetKind.Color ? "Editable global color preset. Preview before applying." :
            "Portable document. Crosshair and display-mode application are not available yet.";
        if (!ReferenceEquals(RefreshUneditedRustPreset(preset), preset))
            return "Earlier Rust starting point with a larger black-level lift. Use Add / update Rust presets for richer color and deeper blacks; Preview and Apply remain manual.";
        return preset.Kind == PresetKind.Color && preset.Color == entry.Preset.Color ? entry.Description :
            "Customized game starting point. Reset selected starting point restores its original values; duplicate first to keep your edits.";
    }
    private static GamePresetStartingPoint Make(string id, string name, string game, ColorSettings color, string description) =>
        new(new() { Id = "builtin-game-" + id, Name = name, GameTag = game, Color = color }, description);
}
