namespace Chromatic.Core.Models;

public sealed record Rational(uint Numerator, uint Denominator);
public sealed record DisplayMode(int Width, int Height, Rational RefreshRate, int BitsPerPixel = 32);
public enum ScalingIntent { Unchanged, Centered, Stretched, AspectRatio }
public sealed record DisplayIntent(int Width, int Height, Rational RefreshRate,
    ScalingIntent Scaling = ScalingIntent.Unchanged, bool? Hdr = null);
