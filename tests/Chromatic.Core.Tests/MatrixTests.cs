using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Xunit;

namespace Chromatic.Core.Tests;
public sealed class MatrixTests
{
    [Fact] public void NeutralSettingsAreIdentityAndGrayscaleUsesInputLumaWeights()
    {
        Assert.Equal(Matrix5x5.Identity.Values.ToArray(), ColorMatrixBuilder.Build(new()).Values.ToArray());
        var red = ColorMatrixBuilder.Saturation(0).Transform(new(1, 0, 0, .25));
        Assert.Equal(.2126, red.R, 12); Assert.Equal(red.R, red.G); Assert.Equal(red.G, red.B); Assert.Equal(.25, red.A);
    }
    [Fact] public void ThreeHundredPercentMatchesNormativeMatrixWithoutCoefficientClipping()
    {
        double[] expected = [2.5748, -.4252, -.4252, 0, 0, -1.4304, 1.5696, -1.4304, 0, 0,
            -.1444, -.1444, 2.8556, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1];
        var matrix = ColorMatrixBuilder.Saturation(300);
        for (var i = 0; i < 25; i++) Assert.Equal(expected[i], matrix.Values[i], 12);
        var red = matrix.Transform(new(1, 0, 0));
        Assert.True(red.R > 1); Assert.True(red.G < 0); Assert.True(red.B < 0);
    }
    [Fact] public void SaturationPreservesEveryNeutralGrayAndAlpha()
    {
        foreach (var percentage in new[] { 0d, 50, 100, 200, 300 })
            for (var i = 0; i < 256; i++)
            {
                var gray = i / 255d; var transformed = ColorMatrixBuilder.Saturation(percentage).Transform(new(gray, gray, gray, .42));
                Assert.Equal(gray, transformed.R, 12); Assert.Equal(gray, transformed.G, 12);
                Assert.Equal(gray, transformed.B, 12); Assert.Equal(.42, transformed.A);
            }
    }
    [Fact] public void FifthRowTranslationAndCompositionMatchAnalyticPipeline()
    {
        var settings = new ColorSettings(175, .1, 1.2, Warmth: .7);
        var input = new Rgba(.2, .4, .6, .33);
        var saturated = ColorMatrixBuilder.Saturation(175).Transform(input);
        var actual = ColorMatrixBuilder.Build(settings).Transform(input);
        Assert.Equal((1.2 * (saturated.R - .5) + .5 + .1) * 1.07, actual.R, 12);
        Assert.Equal(1.2 * (saturated.G - .5) + .5 + .1, actual.G, 12);
        Assert.Equal((1.2 * (saturated.B - .5) + .5 + .1) * .93, actual.B, 12);
        Assert.Equal(.33, actual.A);
        var translated = ColorMatrixBuilder.Build(new(Brightness: .1));
        Assert.Equal(.1, translated[4, 0]); Assert.Equal(0, translated[0, 4]);
    }
    [Fact] public void SoftwareVibranceCapsAtThreeAndNativeRouteDoesNotApplyItTwice()
    {
        var settings = new ColorSettings(250, Vibrance: .8);
        Assert.Equal(ColorMatrixBuilder.Saturation(300).Values.ToArray(), ColorMatrixBuilder.Build(settings).Values.ToArray());
        Assert.Equal(ColorMatrixBuilder.Saturation(250).Values.ToArray(), ColorMatrixBuilder.Build(settings, false).Values.ToArray());
    }
    [Fact] public void BasicLiftPreservesWhiteAndIsNotShadowOnly()
    {
        var matrix = ColorMatrixBuilder.BasicLift(.15);
        Assert.Equal(1, matrix.Transform(new(1, 1, 1)).R, 12);
        Assert.Equal(.15, matrix.Transform(new(0, 0, 0)).R, 12);
        Assert.True(matrix.Transform(new(.8, .8, .8)).R > .8);
    }
    [Fact] public void RejectsInvalidCoefficientsAndAccessibilityStrength()
    {
        Assert.Throws<ArgumentException>(() => new Matrix5x5([1, 2]));
        Assert.Throws<ArgumentException>(() => new Matrix5x5(Enumerable.Repeat(double.NaN, 25)));
        Assert.Throws<ArgumentException>(() => ColorMatrixBuilder.Saturation(double.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => ColorMatrixBuilder.BasicLift(.16));
        Assert.Throws<ArgumentException>(() => ColorMatrixBuilder.Build(new(CvdMode: "RedWeak", CvdStrength: 1.1)));
        Assert.Throws<ArgumentException>(() => new Matrix5x5(Enumerable.Repeat(double.MaxValue, 25)).ToSinglePrecision());
    }
}
