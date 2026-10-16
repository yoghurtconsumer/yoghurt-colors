using Chromatic.Core.Color;
using Chromatic.Native;
using Xunit;

namespace Chromatic.Integration.Tests;
public sealed class AbiTests
{
    [Theory]
    [InlineData(69u, 0u, false)] // Actual build 26200: legacy flags=5, modern flags=69, SDR.
    [InlineData(0u, 0u, false)]
    [InlineData(0xF1u, 0u, false)] // Supported/user-enabled does not mean currently active.
    [InlineData(2u, 0u, true)] // Inconsistent active flag still blocks conservatively.
    [InlineData(0u, 1u, true)]
    [InlineData(0u, 2u, true)]
    public void ModernColorProbeUsesActiveModeInsteadOfLegacyOrCapabilityFlags(uint flags, uint mode, bool advanced)
        => Assert.Equal(advanced, DisplaySafetyProbe.IsModernAdvancedColor(flags, mode));
    [Fact] public void UnknownModernColorModeIsNeverTreatedAsSdr()
        => Assert.Throws<ArgumentOutOfRangeException>(() => DisplaySafetyProbe.IsModernAdvancedColor(0, 3));
    [Fact] public void NativeSizesOffsetsAndMatrixOrientationMatchTheSdk()
    {
        AbiAssertions.Verify();
        DisplaySafetyProbe.VerifyAbi();
        var native = MagColorEffect.FromMatrix(ColorMatrixBuilder.Saturation(300));
        var matrix = native.ToMatrix();
        Assert.Equal((double)(float)-.4252, matrix[0, 1]);
        Assert.Equal((double)(float)-1.4304, matrix[1, 0]);
        Assert.Equal(1, matrix[3, 3]);
    }
    [Fact] public void GammaChannelsRoundTripWithoutInterleaving()
    {
        var managed = new GammaRamp(Enumerable.Range(0, 768).Select(i => (ushort)(i + 100)));
        Assert.Equal(managed.ToBytes().ToArray(), NativeGammaRamp.FromRamp(managed).ToRamp().ToBytes().ToArray());
    }
    [Fact] public void NativeDvcMappingUsesDiscoveredAsymmetricRanges()
    {
        var info = new NvDisplayDvcInfo { Version = NvDisplayDvcInfo.Version1, MinLevel = -50, DefaultLevel = 20, MaxLevel = 90, CurrentLevel = 20 };
        Assert.Equal(-50, info.Map(0)); Assert.Equal(20, info.Map(.5)); Assert.Equal(90, info.Map(1));
        Assert.Throws<ArgumentException>(() => info.Map(double.NaN));
        Assert.Throws<ArgumentException>(() => (info with { MaxLevel = 0 }).Map(.5));
    }
}
