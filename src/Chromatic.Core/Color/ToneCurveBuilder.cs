using Chromatic.Core.Validation;

namespace Chromatic.Core.Color;

public static class ToneCurveBuilder
{
    public static double DarkBoost(double x, double strength, double gammaBlend = 0, double exponent = 1)
    {
        SettingsValidator.Range(x, 0, 1, nameof(x));
        SettingsValidator.Range(strength, 0, .8, nameof(strength));
        SettingsValidator.Range(gammaBlend, 0, 1, nameof(gammaBlend));
        SettingsValidator.Range(exponent, .4, 1, nameof(exponent));
        var lifted = Lift(x, strength);
        return (1 - gammaBlend) * lifted + gammaBlend * Math.Pow(lifted, exponent);
    }
    public static double Map(double x, double gamma = 1, double shadows = 0, double highlights = 0, double holdStrength = 0)
    {
        SettingsValidator.Range(x, 0, 1, nameof(x));
        SettingsValidator.Range(gamma, .5, 2.5, nameof(gamma));
        SettingsValidator.Range(shadows, 0, .8, nameof(shadows));
        SettingsValidator.Range(highlights, 0, .8, nameof(highlights));
        SettingsValidator.Range(holdStrength, 0, .8, nameof(holdStrength));
        var normal = Math.Pow(x, 1 / gamma);
        var lifted = Lift(normal, shadows);
        var highlighted = 1 - Lift(1 - lifted, highlights);
        return Lift(highlighted, holdStrength);
    }
    private static double Lift(double x, double strength)
    {
        if (x >= .45) return x;
        var remaining = 1 - x / .45;
        return x + strength * x * remaining * remaining;
    }
}
