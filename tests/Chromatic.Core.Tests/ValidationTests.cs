using Chromatic.Core.Models;
using Chromatic.Core.Validation;
using Xunit;
namespace Chromatic.Core.Tests;
public sealed class ValidationTests
{
    [Theory] [InlineData(-1)] [InlineData(301)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidSaturation(double value) => Assert.Throws<ArgumentException>(() => SettingsValidator.ValidateColor(new(value)));
    [Fact] public void ValidatesAllIndependentRanges()
    {
        ColorSettings[] invalid = [new(Brightness: .26), new(Contrast: 2.1), new(Gamma: 0), new(Warmth: -2),
            new(Vibrance: 2), new(Shadows: .81), new(Highlights: -.1), new(CvdMode: "Unknown")];
        foreach (var color in invalid) Assert.Throws<ArgumentException>(() => SettingsValidator.ValidateColor(color));
        SettingsValidator.ValidateColor(new(300, -.25, 2, 2.5, 1, -1, .8, .8, "RedWeak"));
    }
    [Fact] public void RejectsInvalidCrosshairAndPortableAssets()
    {
        Assert.Throws<ArgumentException>(() => SettingsValidator.ValidateCrosshair(new(Thickness: 13)));
        Assert.Throws<ArgumentException>(() => SettingsValidator.ValidateCrosshair(new(AssetHash: "C:\\secret.png")));
        Assert.Throws<ArgumentException>(() => SettingsValidator.ValidateCrosshair(new(Shape: CrosshairShape.Picture)));
        Assert.Throws<ArgumentException>(() => PresetValidator.Validate(new() { Kind = PresetKind.Resolution }));
        Assert.Throws<ArgumentException>(() => PresetValidator.Validate(new() { Assets = ["https://example.test/a.png"] }));
    }
    [Fact] public void RationalRefreshIsNotRoundedToIntegerHz()
    {
        SettingsValidator.ValidateIntent(new(1920, 1080, new(60000, 1001)));
        Assert.Throws<ArgumentException>(() => SettingsValidator.ValidateIntent(new(1920, 1080, new(60, 0))));
        Assert.Throws<ArgumentException>(() => SettingsValidator.ValidateIntent(new(1920, 1080, new(999999, 1))));
    }
    [Fact] public void RemoteOnlyAllowsColorAndKnownScenesWithIncreasingSequence()
    {
        var scenes = new HashSet<string> { "night" };
        RemoteCommandValidator.Validate(new(1, 2, 0, RemoteAction.ActivateScene, PresetId: "night"), 1, scenes);
        Assert.Throws<ArgumentException>(() => RemoteCommandValidator.Validate(new(1, 1, 0, RemoteAction.GetState), 1, scenes));
        Assert.Throws<ArgumentException>(() => RemoteCommandValidator.Validate(new(1, 2, 0, RemoteAction.ActivateScene, PresetId: "missing"), 1, scenes));
        Assert.Throws<ArgumentException>(() => RemoteCommandValidator.Validate(new(1, 2, 0, (RemoteAction)999), 1, scenes));
    }
}
