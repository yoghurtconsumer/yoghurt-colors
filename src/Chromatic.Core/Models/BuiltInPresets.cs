using System.Collections.Immutable;
namespace Chromatic.Core.Models;

public static class BuiltInPresets
{
    public static ImmutableArray<PresetDocument> Create() => [
        Make("neutral", "Neutral", new()), Make("treeline", "Treeline", new(135, Contrast: 1.08, Warmth: -.05)),
        Make("interior", "Interior", new(120, Brightness: .04, Contrast: .95)),
        Make("night", "Night", new(110, Brightness: .07, Contrast: .9)),
        Make("snow", "Snow glare", new(120, Brightness: -.03, Contrast: 1.04, Warmth: .05)),
        Make("smoke", "Smoke / dust", new(130, Contrast: 1.15)),
        Make("sky", "Bright sky", new(125, Brightness: -.015)) ];
    private static PresetDocument Make(string id, string name, ColorSettings color) => new()
    { Id = "builtin-" + id, Name = name, Color = color, GameTag = "Original starting point" };
}
