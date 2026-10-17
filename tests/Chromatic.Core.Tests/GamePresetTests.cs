using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Chromatic.Core.Validation;
using Xunit;

namespace Chromatic.Core.Tests;

public sealed class GamePresetTests
{
    [Theory]
    [InlineData("forest", 135, .015, .96)]
    [InlineData("desert", 120, -.01, .96)]
    [InlineData("snow", 125, -.015, .94)]
    [InlineData("tundra", 145, .005, .98)]
    [InlineData("jungle", 125, .03, .92)]
    public void RevisedRustDefaultsHaveDeeperBlacksAndRicherColorAndRefreshOnlyTheKnownOldDefault(string suffix, double saturation, double brightness, double contrast)
    {
        var current = GamePresetCatalog.FindOriginal("builtin-game-rust-" + suffix)!;
        var previous = current with { Color = new(saturation, Brightness: brightness, Contrast: contrast) };
        var oldMatrix = ColorMatrixBuilder.Build(previous.Color!); var matrix = ColorMatrixBuilder.Build(current.Color!);
        var black = new Rgba(0, 0, 0); var muted = new Rgba(.42, .27, .18);
        Assert.InRange(matrix.Transform(black).R, -1e-12, .0061);
        Assert.True(matrix.Transform(black).R < oldMatrix.Transform(black).R);
        var oldColor = oldMatrix.Transform(muted); var newColor = matrix.Transform(muted);
        Assert.True(newColor.R - newColor.B > oldColor.R - oldColor.B);
        Assert.InRange(current.Color!.SaturationPercent, 160, 195); Assert.InRange(current.Color.Contrast, .98, 1);
        Assert.Equal(current, GamePresetCatalog.RefreshUneditedRustPreset(previous));
        Assert.Same(current, GamePresetCatalog.RefreshUneditedRustPreset(current));
        Assert.Contains("Earlier Rust", GamePresetCatalog.Describe(previous));
    }
    [Fact] public void RustRefreshPreservesCustomizationsAndDoesNotAlterTheUsersFavoriteSiegeStrongValues()
    {
        var original = GamePresetCatalog.FindOriginal("builtin-game-rust-forest")!;
        var earlier = original with { Color = new(135, Brightness: .015, Contrast: .96) };
        foreach (var edited in new[]
        {
            earlier with { Name = "My forest" }, earlier with { GameTag = "My game" }, earlier with { Id = "user-copy" },
            earlier with { Color = new(177) }, earlier with { AuthorMetadata = new("Local author", DateTimeOffset.UnixEpoch) },
            earlier with { Assets = [new string('a', 64)] }
        }) Assert.Same(edited, GamePresetCatalog.RefreshUneditedRustPreset(edited));
        var siege = GamePresetCatalog.FindOriginal("builtin-game-siege-color")!;
        Assert.Equal(new ColorSettings(200, Brightness: .005, Contrast: .98), siege.Color);
        Assert.Same(siege, GamePresetCatalog.RefreshUneditedRustPreset(siege));
    }
    [Fact] public void GamePackIsPortableAffineOnlyAndRetainsNeutralGraySteps()
    {
        var presets = GamePresetCatalog.Presets;
        Assert.Equal(14, presets.Length); Assert.Equal(14, presets.Select(p => p.Id).Distinct().Count());
        Assert.Equal(6, presets.Count(p => p.GameTag == GamePresetCatalog.Siege));
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
    [Fact] public void RequestedSiegeStylesHaveDistinctRichColorsWithoutAHeavyBlackLift()
    {
        var champ = GamePresetCatalog.FindOriginal("builtin-game-siege-champ")!;
        var diamond = GamePresetCatalog.FindOriginal("builtin-game-siege-diamond1")!;
        var strong = GamePresetCatalog.FindOriginal("builtin-game-siege-color")!;
        Assert.Equal("Siege — Champ", champ.Name); Assert.Equal("Siege — Im Diamond 1", diamond.Name);
        Assert.Equal(GamePresetCatalog.Siege, champ.GameTag); Assert.Equal(GamePresetCatalog.Siege, diamond.GameTag);
        Assert.True(champ.Color!.SaturationPercent > strong.Color!.SaturationPercent);
        Assert.True(diamond.Color!.SaturationPercent < champ.Color.SaturationPercent);
        var matrix = ColorMatrixBuilder.Build(champ.Color); var black = matrix.Transform(new(0, 0, 0));
        Assert.InRange(black.R, 0, .006); Assert.InRange(champ.Color.Contrast, .99, 1);
        Assert.InRange(matrix.Transform(new(1, 1, 1)).R, .99, 1 + 1e-12);
        var dark = new Rgba(.05, .05, .05); Assert.True(matrix.Transform(dark).R >= dark.R);
        Assert.True(ColorMatrixBuilder.Build(diamond.Color).Transform(new(0, 0, 0)).R > black.R);
        Assert.Equal(new ColorSettings(200, Brightness: .005, Contrast: .98), strong.Color);
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
