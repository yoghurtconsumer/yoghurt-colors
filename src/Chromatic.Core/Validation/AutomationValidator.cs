using Chromatic.Core.Models;
namespace Chromatic.Core.Validation;

public static class AutomationValidator
{
    public static void Validate(AutomationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SchemaVersion != 1 || settings.Profiles.IsDefault || settings.Profiles.Length > 32 ||
            settings.Rules.IsDefault || settings.Rules.Length > 128 || settings.Hotkeys.IsDefault || settings.Hotkeys.Length > 24)
            throw new ArgumentException("Invalid automation version or collection bounds.");
        ValidateHold(settings.HoldKeys);
        if (!settings.MouseHolds && settings.Boost.Enabled && settings.HoldKeys.Any(k => k is 5 or 6))
            throw new ArgumentException("Mouse 4/5 holds need their separate mouse-listener opt-in.");
        SettingsValidator.Validate(new() { HoldBoost = settings.Boost });
        SettingsValidator.ValidateProfile(settings.Desktop);
        if (settings.Desktop.DisplayIntent is not null || settings.Desktop.Crosshair.Enabled)
            throw new ArgumentException("Automation is color-only; display preview and overlay belong to later stages.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in settings.Profiles)
        {
            SettingsValidator.ValidateProfile(profile);
            if (!ids.Add(profile.Id) || profile.DisplayIntent is not null || profile.Crosshair.Enabled)
                throw new ArgumentException("Profiles need unique IDs and color-only intentions.");
        }
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var rules = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in settings.Rules)
        {
            SettingsValidator.Text(rule.Id, 128, "rule ID"); SettingsValidator.Text(rule.ExecutablePath, 32768, "executable path");
            var path = rule.ExecutablePath;
            if (!rules.Add(rule.Id) || !paths.Add(path) || !ids.Contains(rule.ProfileId) || path.Length < 4 ||
                !char.IsAsciiLetter(path[0]) || path[1..3] != @":\" || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                path[2..].Contains(':') || path.Any(c => c is '/' or '"' or '<' or '>' or '|' or '*' or '?') || path.Split('\\').Any(p => p is "" or "." or ".."))
                throw new ArgumentException("Game rules need unique exact local .exe paths, IDs and an existing Color profile.");
        }
        var chords = new HashSet<(uint, int)>(); var hotkeys = new HashSet<int>();
        foreach (var key in settings.Hotkeys)
            if (key.Id is < 1 or > 24 || !hotkeys.Add(key.Id) || !chords.Add((key.Modifiers, key.Key)) || key.Modifiers is 0 or > 7 ||
                key.Key is < 8 or > 254 or 16 or 17 or 18 or 91 or 92 or 123 or >= 160 and <= 165 || !Enum.IsDefined(key.Action) ||
                key.Action == HotkeyAction.Preset && (key.ProfileId is null || !ids.Contains(key.ProfileId)))
                throw new ArgumentException("Hotkeys require unique Ctrl/Alt/Shift chords and a valid action/profile. Windows keys and F12 are reserved.");
    }
    public static void ValidateHold(System.Collections.Immutable.ImmutableArray<int> keys)
    {
        if (keys.IsDefaultOrEmpty || keys.Length > 4 || keys.Distinct().Count() != keys.Length ||
            keys.Any(k => k is < 5 or > 254 or 91 or 92 or >= 160 and <= 165) || keys.All(k => k is 16 or 17 or 18))
            throw new ArgumentException("Hold needs 1–4 distinct keys, including a non-modifier. Use generic Ctrl/Alt/Shift; Windows keys are excluded.");
    }
}
