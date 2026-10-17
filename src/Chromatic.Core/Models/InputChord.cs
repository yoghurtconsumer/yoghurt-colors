using System.Collections.Immutable;
using Chromatic.Core.Validation;
namespace Chromatic.Core.Models;

public static class InputChord
{
    public static ImmutableArray<int> Parse(string text)
    {
        if (text.Length > 64) throw new ArgumentException("Chord is too long.");
        var keys = text.Split('+', StringSplitOptions.TrimEntries).Select(Key).ToImmutableArray();
        AutomationValidator.ValidateHold(keys); return keys;
    }
    public static HotkeyBinding Hotkey(int id, string text, HotkeyAction action, string? profileId)
    {
        var keys = Parse(text); uint modifiers = 0;
        foreach (var k in keys) modifiers |= k switch { 16 => 4u, 17 => 2u, 18 => 1u, _ => 0u };
        var normal = keys.Where(k => k is not 16 and not 17 and not 18).ToArray();
        if (normal.Length != 1 || normal[0] is 5 or 6 || modifiers == 0) throw new ArgumentException("Hotkey needs Ctrl/Alt/Shift plus one keyboard key.");
        return new(id, modifiers, normal[0], action, profileId);
    }
    public static string Format(IEnumerable<int> keys) => string.Join('+', keys.Select(k => k switch
    { 16 => "Shift", 17 => "Ctrl", 18 => "Alt", 5 => "Mouse4", 6 => "Mouse5", 9 => "Tab", 32 => "Space", 27 => "Escape", 13 => "Enter", >= 112 and <= 135 => "F" + (k - 111),
        >= 65 and <= 90 or >= 48 and <= 57 => ((char)k).ToString(), _ => "VK" + k }));
    private static int Key(string token)
    {
        var key = token.ToUpperInvariant();
        if (key is "CTRL" or "CONTROL") return 17; if (key == "ALT") return 18; if (key == "SHIFT") return 16;
        if (key == "MOUSE4") return 5; if (key == "MOUSE5") return 6;
        if (key == "TAB") return 9; if (key == "SPACE") return 32; if (key == "ESCAPE") return 27; if (key == "ENTER") return 13;
        if (key.StartsWith("VK", StringComparison.Ordinal) && int.TryParse(key[2..], out var vk) && vk is >= 5 and <= 254) return vk;
        if (key.Length == 1 && char.IsAsciiLetterOrDigit(key[0])) return key[0];
        if (key.StartsWith('F') && int.TryParse(key[1..], out var function) && function is >= 1 and <= 24) return 111 + function;
        throw new ArgumentException("Use Ctrl/Alt/Shift, letters, digits, F1–F24 or Mouse4/Mouse5.");
    }
}
