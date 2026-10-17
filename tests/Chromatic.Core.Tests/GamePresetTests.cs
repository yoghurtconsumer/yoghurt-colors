using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Chromatic.Core.Validation;
using Xunit;

namespace Chromatic.Core.Tests;

public sealed class GamePresetTests
{
    [Fact] public void GamePackIsPortableAffineOnlyAndRetainsNeutralGraySteps()
    {
        var presets = GamePresetCatalog.Presets;
        Assert.Equal(12, presets.Length); Assert.Equal(12, presets.Select(p => p.Id).Distinct().Count());
        Assert.Equal(4, presets.Count(p => p.GameTag == GamePresetCatalog.Siege));
        foreach (var preset in presets)
        {
            PresetValidator.Validate(preset); Assert.Equal(PresetKind.Color, preset.Kind);
            var color = Assert.IsType<ColorSettings>(preset.Color);
            Assert.Equal(1, color.Gamma); Assert.Equal(0, color.Shadows); Assert.Equal(0, color.Highlights);
            Assert.Equal(0, color.Vibrance); Assert.Equal("None", color.CvdMode);
            Assert.Null(preset.Crosshair); Assert.Null(preset.DisplayIntent); Assert.Empty(preset.Assets);
            var matrix = ColorMatrixBuilder.Build(color); var previous = double.NegativeInfinity;
            for (var step = 0; step <= 255; step++)
            {
                var gray = step / 255d; var result = matrix.Transform(new(gray, gray, gray, .42));
                Assert.InRange(result.R, -1e-12, 1 + 1e-12);
                Assert.Equal(result.R, result.G, 12); Assert.Equal(result.G, result.B, 12);
                Assert.True(result.R > previous, $"{preset.Name} crushed a neutral gray step.");
                Assert.Equal(.42, result.A); previous = result.R;
            }
        }
    }
    [Fact] public void RustCoversFiveEnvironmentsWithDistinctTransformsAndUsefulToneDifferences()
    {
        var presets = GamePresetCatalog.Presets.Where(p => p.GameTag == GamePresetCatalog.Rust).ToArray();
        Assert.Equal(new[] { "arctic", "arid", "forest", "jungle", "tundra" }, presets.Select(p => p.Id switch
        {
            "builtin-game-rust-snow" => "arctic", "builtin-game-rust-desert" => "arid", "builtin-game-rust-forest" => "forest",
            "builtin-game-rust-jungle" => "jungle", "builtin-game-rust-tundra" => "tundra", _ => "unexpected"
        }).Order());
        Assert.Equal(5, presets.Select(p => p.Color).Distinct().Count());
        Matrix5x5 Matrix(string id) => ColorMatrixBuilder.Build(presets.Single(p => p.Id == "builtin-game-rust-" + id).Color!);
        var forest = Matrix("forest"); var jungle = Matrix("jungle"); var snow = Matrix("snow"); var desert = Matrix("desert");
        var dark = new Rgba(.05, .05, .05); var white = new Rgba(1, 1, 1);
        Assert.True(jungle.Transform(dark).R > forest.Transform(dark).R);
        Assert.True(snow.Transform(white).R < desert.Transform(white).R);
        Assert.True(desert.Transform(white).R < forest.Transform(white).R);
        Assert.Contains("whole-image", GamePresetCatalog.Describe(presets.Single(p => p.Id.EndsWith("jungle"))));
    }
    [Fact] public void IndoorStartingPointLiftsDarkNeutralsWhileKeepingWhiteWithinRange()
    {
        var preset = GamePresetCatalog.Presets.Single(p => p.Id == "builtin-game-siege-indoor");
        var matrix = ColorMatrixBuilder.Build(preset.Color!);
        foreach (var input in new[] { 0d, .02, .05, .1, .2 })
            Assert.True(matrix.Transform(new(input, input, input)).R > input);
        Assert.InRange(matrix.Transform(new(1, 1, 1)).R, .98, 1);
        Assert.Contains("whole image", GamePresetCatalog.Describe(preset));
        Assert.Contains("not a shadow-only", GamePresetCatalog.Describe(preset));
        Assert.Contains("Customized", GamePresetCatalog.Describe(preset with { Color = new(180) }));
    }
}
