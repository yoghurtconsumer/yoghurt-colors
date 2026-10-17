namespace Chromatic.Core.Models;

public sealed record ColorSettings(double SaturationPercent = 100,
    double Brightness = 0, double Contrast = 1, double Gamma = 1, double Warmth = 0,
    double Vibrance = 0, double Shadows = 0, double Highlights = 0, string CvdMode = "None", double CvdStrength = 1)
{
    // Optional local affine extensions. Omitted at zero so existing v1 documents
    // and the ten-control remote whitelist keep their original wire shape.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public double HueRotationDegrees { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public double DarkenHueDegrees { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public double DarkenStrength { get; init; }
    public static ColorSettings Neutral { get; } = new();
}

public sealed record HoldBoostSettings(bool Enabled = false, double Strength = .6,
    double RedAttenuation = 0, double GreenAttenuation = 0, double BlueAttenuation = 0);

// The matrix is desktop-wide. Only these independent SDR intentions target a monitor.
public sealed record MonitorColorSettings(DisplayIdentity Display, double Gamma = 1, double Warmth = 0);
