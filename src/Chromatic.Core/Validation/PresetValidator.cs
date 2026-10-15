using Chromatic.Core.Models;

namespace Chromatic.Core.Validation;

public static class PresetValidator
{
    public static void Validate(PresetDocument preset)
    {
        if (preset.SchemaVersion != 1 || !Enum.IsDefined(preset.Kind)) throw new ArgumentException("Unknown preset version/kind.");
        SettingsValidator.Text(preset.Id, 128, "preset ID"); SettingsValidator.Text(preset.Name, 128, "preset name");
        if (preset.Color is not null) SettingsValidator.ValidateColor(preset.Color);
        if (preset.Crosshair is not null) SettingsValidator.ValidateCrosshair(preset.Crosshair);
        if (preset.DisplayIntent is not null) SettingsValidator.ValidateIntent(preset.DisplayIntent);
        if (preset.Kind == PresetKind.Color && (preset.Color is null || preset.Crosshair is not null || preset.DisplayIntent is not null) ||
            preset.Kind == PresetKind.Crosshair && (preset.Crosshair is null || preset.Color is not null || preset.DisplayIntent is not null) ||
            preset.Kind == PresetKind.Resolution && (preset.DisplayIntent is null || preset.Color is not null || preset.Crosshair is not null) ||
            preset.Kind == PresetKind.FullSetup && (preset.Color is null || preset.Crosshair is null || preset.DisplayIntent is null))
            throw new ArgumentException("Preset payload does not match kind.");
        if (preset.GameTag is not null) SettingsValidator.Text(preset.GameTag, 128, "game tag");
        if (preset.AuthorMetadata is not null) SettingsValidator.Text(preset.AuthorMetadata.Name, 128, "author");
        if (preset.Assets.IsDefault || preset.Assets.Length > 16 || preset.Assets.Any(a => !SettingsValidator.Hash().IsMatch(a)))
            throw new ArgumentException("Invalid asset identifiers.");
        if (preset.Crosshair?.AssetHash is string hash && !preset.Assets.Contains(hash))
            throw new ArgumentException("Crosshair asset missing from manifest.");
    }
}
