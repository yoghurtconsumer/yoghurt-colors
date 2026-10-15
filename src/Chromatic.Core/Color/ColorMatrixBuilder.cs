using Chromatic.Core.Models;
using Chromatic.Core.Validation;

namespace Chromatic.Core.Color;

public static class ColorMatrixBuilder
{
    public static Matrix5x5 Saturation(double percent)
    {
        SettingsValidator.Range(percent, 0, 300, nameof(percent));
        var values = Matrix5x5.Identity.Values.ToArray();
        double[] weights = [.2126, .7152, .0722];
        var factor = percent / 100;
        for (var input = 0; input < 3; input++)
            for (var output = 0; output < 3; output++)
                values[input * 5 + output] = (1 - factor) * weights[input] + (input == output ? factor : 0);
        return new(values);
    }
    public static Matrix5x5 Build(ColorSettings settings, bool softwareVibrance = true)
    {
        SettingsValidator.ValidateColor(settings);
        if (settings.CvdMode != "None")
            throw new NotSupportedException("Experimental accessibility correction awaits checked and attributed simulation coefficients.");
        var saturation = softwareVibrance ? Math.Min(300, settings.SaturationPercent * (1 + settings.Vibrance)) : settings.SaturationPercent;
        var affine = Matrix5x5.Identity.Values.ToArray();
        var warmth = Matrix5x5.Identity.Values.ToArray();
        for (var channel = 0; channel < 3; channel++)
        {
            affine[channel * 5 + channel] = settings.Contrast;
            affine[20 + channel] = .5 * (1 - settings.Contrast) + settings.Brightness;
        }
        warmth[0] = 1 + .1 * settings.Warmth;
        warmth[12] = 1 - .1 * settings.Warmth;
        // Gamma/shadows/highlights are deliberately independent nonlinear LUT operations.
        return Saturation(saturation) * new Matrix5x5(affine) * new Matrix5x5(warmth);
    }
    public static Matrix5x5 BasicLift(double lift)
    {
        SettingsValidator.Range(lift, 0, .15, nameof(lift));
        var values = Matrix5x5.Identity.Values.ToArray();
        for (var channel = 0; channel < 3; channel++) { values[channel * 5 + channel] = 1 - lift; values[20 + channel] = lift; }
        return new(values);
    }
}
