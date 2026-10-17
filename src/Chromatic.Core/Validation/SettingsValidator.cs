using System.Text.RegularExpressions;
using Chromatic.Core.Models;

namespace Chromatic.Core.Validation;

public static partial class SettingsValidator
{
    public static void Range(double value, double min, double max, string name)
    {
        if (!double.IsFinite(value) || value < min || value > max)
            throw new ArgumentException($"{name} must be finite and within [{min}, {max}].");
    }
    public static void Text(string value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.Any(char.IsControl))
            throw new ArgumentException($"Invalid {name}.");
    }
    public static void Validate(AppSettings settings)
    {
        if (settings.SchemaVersion != 1) throw new ArgumentException("Unknown settings schema version.");
        ValidateColor(settings.GlobalColor);
        ValidateMonitors(settings.Monitors);
        ValidateCrosshair(settings.Crosshair);
        Range(settings.HoldBoost.Strength, 0, .8, "Hold strength");
        Range(settings.HoldBoost.RedAttenuation, 0, .1, "Red tint");
        Range(settings.HoldBoost.GreenAttenuation, 0, .1, "Green tint");
        Range(settings.HoldBoost.BlueAttenuation, 0, .1, "Blue tint");
        if (!Enum.IsDefined(settings.CloseBehavior) || !Enum.IsDefined(settings.Theme))
            throw new ArgumentException("Unknown settings enum.");
    }
    public static void ValidateColor(ColorSettings color)
    {
        ArgumentNullException.ThrowIfNull(color);
        Range(color.SaturationPercent, 0, 300, nameof(color.SaturationPercent));
        Range(color.Brightness, -.25, .25, nameof(color.Brightness));
        Range(color.Contrast, .5, 2, nameof(color.Contrast));
        Range(color.Gamma, .5, 2.5, nameof(color.Gamma));
        Range(color.Warmth, -1, 1, nameof(color.Warmth));
        // Normalized intention, not a fabricated NVIDIA driver range.
        Range(color.Vibrance, -1, 1, nameof(color.Vibrance));
        Range(color.Shadows, 0, .8, nameof(color.Shadows));
        Range(color.Highlights, 0, .8, nameof(color.Highlights));
        Range(color.CvdStrength, 0, 1, nameof(color.CvdStrength));
        Range(color.HueRotationDegrees, -180, 180, nameof(color.HueRotationDegrees));
        Range(color.DarkenHueDegrees, 0, 360, nameof(color.DarkenHueDegrees));
        Range(color.DarkenStrength, 0, 4, nameof(color.DarkenStrength));
        if (color.CvdMode is not ("None" or "RedWeak" or "GreenWeak" or "BlueWeak"))
            throw new ArgumentException("Unknown color accessibility mode.");
    }
    public static void ValidateCrosshair(CrosshairSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!Enum.IsDefined(value.Shape)) throw new ArgumentException("Unknown crosshair shape.");
        Range(value.Size, 2, 128, "Size"); Range(value.Gap, 0, 64, "Gap");
        Range(value.Thickness, 1, 12, "Thickness"); Range(value.Outline, 0, 6, "Outline");
        Range(value.Opacity, 0, 1, "Opacity");
        Range(value.OffsetX, -32768, 32768, "OffsetX"); Range(value.OffsetY, -32768, 32768, "OffsetY");
        if (!Argb().IsMatch(value.Color)) throw new ArgumentException("Expected #AARRGGBB color.");
        if (value.AssetHash is not null && !Hash().IsMatch(value.AssetHash))
            throw new ArgumentException("Asset must be a lowercase SHA-256 identifier, never a path or URL.");
        if (value.Shape == CrosshairShape.Picture && value.AssetHash is null)
            throw new ArgumentException("Picture crosshair requires a validated local asset.");
    }
    public static void ValidateDisplay(DisplayIdentity display)
    {
        ArgumentNullException.ThrowIfNull(display);
        Text(display.MonitorDevicePath, 1024, "monitor device path");
        Text(display.GdiName, 128, "GDI name");
        if (display.EdidSerial is not null) Text(display.EdidSerial, 128, "EDID serial");
    }
    public static void ValidateIntent(DisplayIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        Range(intent.Width, 320, 16384, "Width"); Range(intent.Height, 200, 16384, "Height");
        Range(intent.RefreshRate.Numerator, 1, 1_000_000, "Refresh numerator");
        Range(intent.RefreshRate.Denominator, 1, 100_000, "Refresh denominator");
        Range((double)intent.RefreshRate.Numerator / intent.RefreshRate.Denominator, 1, 1000, "Refresh Hz");
        if (!Enum.IsDefined(intent.Scaling)) throw new ArgumentException("Unknown scaling intent.");
    }
    public static void ValidateProfile(DisplayProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.SchemaVersion != 1) throw new ArgumentException("Unknown profile schema version.");
        Text(profile.Id, 128, "profile ID"); Text(profile.Name, 128, "profile name");
        ValidateColor(profile.GlobalColor); ValidateCrosshair(profile.Crosshair);
        ValidateMonitors(profile.Monitors);
        if (profile.DisplayIntent is not null) ValidateIntent(profile.DisplayIntent);
    }
    private static void ValidateMonitors(System.Collections.Immutable.ImmutableArray<MonitorColorSettings> monitors)
    {
        if (monitors.IsDefault || monitors.Length > 32) throw new ArgumentException("Invalid monitor list.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in monitors)
        {
            ValidateDisplay(item.Display);
            if (!paths.Add(item.Display.MonitorDevicePath)) throw new ArgumentException("Duplicate monitor identity.");
            Range(item.Gamma, .5, 2.5, "Per-monitor gamma"); Range(item.Warmth, -1, 1, "Per-monitor warmth");
        }
    }
    [GeneratedRegex("^#[0-9A-Fa-f]{8}$", RegexOptions.CultureInvariant)] private static partial Regex Argb();
    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)] public static partial Regex Hash();
}
