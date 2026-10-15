using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Xunit;

namespace Chromatic.Core.Tests;
public sealed class ToneCurveTests
{
    private static GammaRamp CapturedCalibration() => new(Enumerable.Range(0, 768).Select(i =>
        (ushort)Math.Round(500 + Math.Pow((i % 256) / 255d, 1.1 + (i / 256) * .1) * 64000)));
    [Fact] public void ShadowCurveHasExactEndpointsAndLeavesHighlightsAlone()
    {
        for (var step = 0; step <= 80; step++)
        {
            var strength = step / 100d;
            Assert.Equal(0, ToneCurveBuilder.DarkBoost(0, strength));
            Assert.Equal(1, ToneCurveBuilder.DarkBoost(1, strength));
            Assert.Equal(.45, ToneCurveBuilder.DarkBoost(.45, strength));
            double previous = 0;
            for (var i = 0; i < 256; i++)
            {
                var x = i / 255d; var y = ToneCurveBuilder.DarkBoost(x, strength);
                Assert.InRange(y, previous, 1); if (x >= .45) Assert.Equal(x, y);
                previous = y;
            }
        }
        Assert.Equal(.45, ToneCurveBuilder.DarkBoost(.45 - 1e-8, .8), 7);
    }
    [Theory] [InlineData(.5)] [InlineData(1)] [InlineData(2.5)]
    public void ComposedTonalCurveRemainsMonotoneWithExactEndpoints(double gamma)
    {
        double previous = 0;
        for (var i = 0; i < 256; i++)
        {
            var value = ToneCurveBuilder.Map(i / 255d, gamma, .8, .8, .8);
            Assert.InRange(value, previous, 1); previous = value;
        }
        Assert.Equal(0, ToneCurveBuilder.Map(0, gamma, .8, .8, .8));
        Assert.Equal(1, ToneCurveBuilder.Map(1, gamma, .8, .8, .8));
    }
    [Fact] public void GammaBaselineRoundTripsEveryByteAndNeutralCompositionPreservesIt()
    {
        var baseline = CapturedCalibration();
        var bytes = baseline.ToBytes().ToArray(); Assert.Equal(1536, bytes.Length);
        Assert.Equal(bytes, GammaRamp.FromBytes(bytes).ToBytes().ToArray());
        Assert.Equal(bytes, GammaRampComposer.Compose(baseline).ToBytes().ToArray());
        Assert.NotEqual((ushort)0, baseline.Values[0]); // actual calibration is not assumed linear
    }
    [Fact] public void HoldReleaseUsesCapturedCalibrationWithoutResidualOrAccumulation()
    {
        var baseline = CapturedCalibration(); var original = baseline.ToBytes().ToArray();
        var held = GammaRampComposer.Compose(baseline, hold: new(true));
        Assert.True(held.Values[20] > baseline.Values[20]); Assert.Equal(baseline.Values[200], held.Values[200]);
        Assert.Equal(original, GammaRampComposer.Compose(baseline, hold: new(false)).ToBytes().ToArray());
        Assert.Equal(original, baseline.ToBytes().ToArray());
        Assert.Equal(held.ToBytes().ToArray(), GammaRampComposer.Compose(baseline, hold: new(true)).ToBytes().ToArray());
    }
    [Fact] public void ToneWarmthAndTintMaintainContiguousChannelOrderAndBounds()
    {
        var baseline = CapturedCalibration();
        var composed = GammaRampComposer.Compose(baseline, gamma: 1.2, shadows: .4, perMonitorWarmth: .5,
            hold: new(true, .6, .1, 0, .05));
        for (var channel = 0; channel < 3; channel++)
            for (var i = 1; i < 256; i++) Assert.True(composed.Values[channel * 256 + i] >= composed.Values[channel * 256 + i - 1]);
        Assert.True(composed.Values[767] < baseline.Values[767]);
        Assert.Equal(baseline.Values[511], composed.Values[511]);
    }
    [Fact] public void RejectsBadCurveInputsAndNonMonotoneCalibration()
    {
        Assert.Throws<ArgumentException>(() => ToneCurveBuilder.DarkBoost(.2, .9));
        Assert.Throws<ArgumentException>(() => ToneCurveBuilder.Map(double.NaN));
        Assert.Throws<ArgumentException>(() => GammaRamp.FromBytes(new byte[1535]));
        var values = CapturedCalibration().Values.ToArray(); values[20] = 0;
        Assert.Throws<ArgumentException>(() => GammaRampComposer.Compose(new(values)));
        Assert.Throws<ArgumentException>(() => GammaRampComposer.Compose(CapturedCalibration(), hold: new(true, RedAttenuation: .11)));
    }
}
