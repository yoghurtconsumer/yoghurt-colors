using Chromatic.Core.Validation;

namespace Chromatic.Core.Color;

// Original affine colour opponent transform, not game/reticle recognition.
// Subtract a chosen chromatic component equally from RGB. Neutral grays have
// zero component, so white/gray/black remain unchanged; opposite hues lighten.
// Values remain signed until final display clipping, exactly like saturation.
public static class ColourAccentBuilder
{
    public static Rgba Hue(double degrees)
    {
        SettingsValidator.Range(degrees, 0, 360, nameof(degrees));
        var sector = degrees % 360 / 60; var secondary = 1 - Math.Abs(sector % 2 - 1);
        return (int)sector switch
        { 0 => new(1, secondary, 0), 1 => new(secondary, 1, 0), 2 => new(0, 1, secondary),
            3 => new(0, secondary, 1), 4 => new(secondary, 0, 1), _ => new(1, 0, secondary) };
    }
    public static Matrix5x5 Darken(double hueDegrees, double strength)
    {
        SettingsValidator.Range(strength, 0, 4, nameof(strength));
        var target = Hue(hueDegrees); if (strength == 0) return Matrix5x5.Identity;
        var mean = (target.R + target.G + target.B) / 3;
        double[] chroma = [target.R - mean, target.G - mean, target.B - mean];
        var denominator = chroma.Sum(v => v * v); // >= 0.5 for this saturated hue circle.
        var values = Matrix5x5.Identity.Values.ToArray();
        for (var input = 0; input < 3; input++)
            for (var output = 0; output < 3; output++)
                values[input * 5 + output] -= strength * chroma[input] / denominator;
        return new(values);
    }
    public static Matrix5x5 Rotate(double degrees)
    {
        SettingsValidator.Range(degrees, -180, 180, nameof(degrees));
        if (degrees == 0) return Matrix5x5.Identity;
        var angle = degrees * Math.PI / 180; var cosine = Math.Cos(angle); var sine = Math.Sin(angle) / Math.Sqrt(3);
        int[,] cross = { { 0, -1, 1 }, { 1, 0, -1 }, { -1, 1, 0 } };
        var values = Matrix5x5.Identity.Values.ToArray();
        for (var input = 0; input < 3; input++)
            for (var output = 0; output < 3; output++)
                values[input * 5 + output] = (input == output ? cosine : 0) + (1 - cosine) / 3 + sine * cross[output, input];
        return new(values);
    }
}
