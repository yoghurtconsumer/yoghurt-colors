using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Chromatic.Infrastructure.Persistence;
using Xunit;

namespace Chromatic.Integration.Tests;
public sealed class RustVividPresetTests
{
    [Fact] public async Task ShippedVividDraftAndCodeAgreeAndLiftDarkStepsWithoutHueDarkening()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "ChromaticDesktop.sln"))) root = root.Parent!;
        var preset = await PresetExchange.ReadFileAsync(Path.Combine(root.FullName, "docs/presets/Rust-Vivid-Clarity.chromaticpreset"));
        var code = await File.ReadAllTextAsync(Path.Combine(root.FullName, "docs/presets/Rust-Vivid-Clarity.cd1.txt"));
        Assert.Equal(preset.Color, PresetExchange.ImportCode(code.Trim()).Color);
        var color = preset.Color!; Assert.Equal(185, color.SaturationPercent); Assert.Equal(0, color.HueRotationDegrees); Assert.Equal(0, color.DarkenStrength);
        Assert.Equal(1, color.Gamma); Assert.Equal(0, color.Shadows); Assert.Equal(0, color.Highlights);
        var matrix = ColorMatrixBuilder.Build(color); var previous = -1d;
        for (var i = 0; i <= 255; i++)
        {
            var v = i / 255d; var result = matrix.Transform(new(v, v, v, .4));
            Assert.InRange(result.R, 0, 1.000000000001); Assert.True(result.R > previous); Assert.Equal(.4, result.A); previous = result.R;
        }
        Assert.Equal(.025, matrix.Transform(new(0, 0, 0)).R, 10);
        Assert.Equal(.07375, matrix.Transform(new(.05, .05, .05)).R, 10);
        Assert.Equal(1, matrix.Transform(new(1, 1, 1)).R, 10);
        Assert.Null(preset.DisplayIntent); Assert.Null(preset.Crosshair); Assert.Empty(preset.Assets);
    }
}
