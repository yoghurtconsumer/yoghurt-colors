using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Core.Validation;
using Xunit;

namespace Chromatic.Core.Tests;

public sealed class ColourAccentTests
{
    [Fact] public void PlannerRoutesColourOnlyDarkeningToGlobalMatrix()
    {
        var state = new EffectsState { Enabled = true, DesktopProfile = new() { GlobalColor = new() { DarkenHueDegrees = 240, DarkenStrength = 1 } },
            Capabilities = new([new(BackendKind.Magnification, Capability.Available, "Fake", EffectScope.GlobalDesktop)]) };
        var plan = BackendPlanner.Plan(state, new()); Assert.Equal(BackendKind.Magnification, Assert.Single(plan.Operations).Backend);
        Assert.Contains(plan.Limitations, l => l.Contains("neutral white remains white"));
    }
    [Fact] public void NonFiniteExtendedControlsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => SettingsValidator.ValidateColor(new() { HueRotationDegrees = double.NaN }));
        Assert.Throws<ArgumentException>(() => SettingsValidator.ValidateColor(new() { DarkenHueDegrees = double.PositiveInfinity }));
        Assert.Throws<ArgumentException>(() => SettingsValidator.ValidateColor(new() { DarkenStrength = double.NaN }));
    }
    [Theory] [InlineData(0)] [InlineData(60)] [InlineData(120)] [InlineData(180)] [InlineData(240)] [InlineData(300)] [InlineData(360)]
    public void ChosenSaturatedHueCanReachBlackWithoutChangingAlpha(double hue)
    {
        var target = ColourAccentBuilder.Hue(hue) with { A = .37 };
        var result = ColourAccentBuilder.Darken(hue, 1).Transform(target);
        Assert.InRange(Math.Clamp(result.R, 0, 1), 0, 1e-12);
        Assert.InRange(Math.Clamp(result.G, 0, 1), 0, 1e-12);
        Assert.InRange(Math.Clamp(result.B, 0, 1), 0, 1e-12); Assert.Equal(.37, result.A);
    }
    [Fact] public void EveryNeutralRemainsUnchangedAtAllSupportedDepthsAndHueRotations()
    {
        foreach (var hue in new[] { 0d, 37, 120, 240, 300, 360 })
            foreach (var depth in new[] { 0d, 1, 4 })
                foreach (var rotation in new[] { -180d, -15, 0, 15, 180 })
                    for (var i = 0; i <= 32; i++)
                    {
                        var v = i / 32d; var settings = new ColorSettings { HueRotationDegrees = rotation, DarkenHueDegrees = hue, DarkenStrength = depth };
                        var result = ColorMatrixBuilder.Build(settings).Transform(new(v, v, v, .31));
                        Assert.Equal(v, result.R, 11); Assert.Equal(v, result.G, 11); Assert.Equal(v, result.B, 11); Assert.Equal(.31, result.A);
                    }
    }
    [Fact] public void RotationIsReversibleAndPositiveOneTwentyCyclesRedToGreen()
    {
        var red = ColourAccentBuilder.Rotate(120).Transform(new(1, 0, 0));
        Assert.Equal(0, red.R, 12); Assert.Equal(1, red.G, 12); Assert.Equal(0, red.B, 12);
        var sample = new Rgba(.13, .67, .82, .4);
        var recovered = (ColourAccentBuilder.Rotate(37) * ColourAccentBuilder.Rotate(-37)).Transform(sample);
        Assert.Equal(sample.R, recovered.R, 12); Assert.Equal(sample.G, recovered.G, 12); Assert.Equal(sample.B, recovered.B, 12); Assert.Equal(sample.A, recovered.A);
    }
    [Fact] public void OppositeColoursChangeTooAndIdenticalWhitesCannotBeDistinguished()
    {
        var matrix = ColourAccentBuilder.Darken(240, 4);
        var blue = matrix.Transform(new(0, 0, 1)); Assert.True(blue.B < 0);
        var red = matrix.Transform(new(1, 0, 0)); Assert.True(red.G > 1); Assert.True(red.B > 1);
        var white = matrix.Transform(new(1, 1, 1)); Assert.Equal(1, white.R, 12); Assert.Equal(1, white.G, 12); Assert.Equal(1, white.B, 12);
    }
    [Theory] [InlineData(-181, 0, 0)] [InlineData(181, 0, 0)] [InlineData(0, -1, 0)] [InlineData(0, 361, 0)] [InlineData(0, 0, -1)] [InlineData(0, 0, 4.01)]
    public void ExtendedRangesAreBounded(double rotation, double hue, double strength) => Assert.Throws<ArgumentException>(() =>
        SettingsValidator.ValidateColor(new() { HueRotationDegrees = rotation, DarkenHueDegrees = hue, DarkenStrength = strength }));
    [Fact] public void HoldReleaseUsesLatestBaseAccentAndNeverPersistsTemporaryLift()
    {
        var latest = new ColorSettings(150) { HueRotationDegrees = 15, DarkenHueDegrees = 255, DarkenStrength = 4 };
        var state = new EffectsState { Enabled = true, Held = true, HoldBoost = new(true) };
        state = EffectsReducer.Reduce(state, new(Guid.NewGuid(), 0, 0, new SetColor(latest))).State;
        Assert.True(state.Held); Assert.Same(latest, state.BaseProfile.GlobalColor);
        state = EffectsReducer.Reduce(state, new(Guid.NewGuid(), state.Revision, state.Generation, new SetHold(false))).State;
        Assert.Null(state.Effective.HoldModifier); Assert.Same(latest, state.Effective.GlobalColor);
    }
}
