using System.Collections.Immutable;

namespace Chromatic.Core.Models;

public sealed record GamePresetStartingPoint(PresetDocument Preset, string Description);

// Original, uncalibrated appearance choices. No game-state input is used.
// Keep LUT-only controls neutral so the pack uses the global affine SDR route.
public static class GamePresetCatalog
{
    public const string Siege = "Rainbow Six Siege";
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
        Make("cs2-balanced", "CS2 — Balanced", "Counter-Strike 2", new(155, Brightness: .01, Contrast: .98),
            "A color-forward starting point with a small overall lift. Compare surfaces, outlines and bright areas on the maps you play."),
        Make("valorant-balanced", "VALORANT — Balanced", "VALORANT", new(135),
            "A restrained color boost. Check your chosen in-game outline color and lower saturation if outlines lose detail."),
        Make("apex-balanced", "Apex — Balanced", "Apex Legends", new(145, Brightness: .008, Contrast: .98),
            "A moderate color boost and small overall lift. Compare indoor areas, sky and terrain under the lighting you play.")
    ];
    public static ImmutableArray<PresetDocument> Presets => Entries.Select(e => e.Preset).ToImmutableArray();
    public static PresetDocument? FindOriginal(string id) =>
        Entries.Select(e => e.Preset).Concat(BuiltInPresets.Create()).FirstOrDefault(p => p.Id == id);
    public static string Describe(PresetDocument? preset)
    {
        if (preset is null) return "Select a preset to see its purpose.";
        var entry = Entries.FirstOrDefault(e => e.Preset.Id == preset.Id);
        if (entry is null) return preset.Kind == PresetKind.Color ? "Editable global color preset. Preview before applying." :
            "Portable document. Crosshair and display-mode application are not available yet.";
        return preset.Kind == PresetKind.Color && preset.Color == entry.Preset.Color ? entry.Description :
            "Customized game starting point. Reset selected starting point restores its original values; duplicate first to keep your edits.";
    }
    private static GamePresetStartingPoint Make(string id, string name, string game, ColorSettings color, string description) =>
        new(new() { Id = "builtin-game-" + id, Name = name, GameTag = game, Color = color }, description);
}
