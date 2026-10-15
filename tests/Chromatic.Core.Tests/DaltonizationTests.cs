using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Xunit;

namespace Chromatic.Core.Tests;
public sealed class DaltonizationTests
{
    [Fact] public void PublishedColumnVectorCoefficientsAreTransposedForRowVectors()
    {
        var red = DaltonizationBuilder.Simulation("RedWeak").Transform(new(1, 0, 0, .4));
        Assert.Equal(.152286, red.R); Assert.Equal(.114503, red.G); Assert.Equal(-.003882, red.B); Assert.Equal(.4, red.A);
    }
    [Theory] [InlineData("RedWeak")] [InlineData("GreenWeak")] [InlineData("BlueWeak")]
    public void StrengthZeroIsIdentityAndCorrectionIsDistinctFromSimulation(string mode)
    {
        Assert.Equal(Matrix5x5.Identity.Values.ToArray(), DaltonizationBuilder.Correction(mode, 0).Values.ToArray());
        var corrected = DaltonizationBuilder.Correction(mode, .5).Transform(new(.8, .2, .4, .3));
        var simulated = DaltonizationBuilder.Simulation(mode).Transform(new(.8, .2, .4, .3));
        Assert.NotEqual(simulated, corrected); Assert.Equal(.3, corrected.A);
        Assert.Equal(DaltonizationBuilder.Correction(mode, .5).Values.ToArray(),
            ColorMatrixBuilder.Build(new(CvdMode: mode, CvdStrength: .5)).Values.ToArray());
    }
    [Fact] public void RedCorrectionMatchesSpecifiedResidualRedistribution()
    {
        var color = DaltonizationBuilder.Correction("RedWeak").Transform(new(1, 0, 0));
        Assert.Equal(1, color.R); Assert.Equal(.7 * (1 - .152286) - .114503, color.G, 12);
        Assert.Equal(.7 * (1 - .152286) + .003882, color.B, 12);
    }
}
