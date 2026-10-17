using System.Collections.Immutable;
namespace Chromatic.Core.Models;

public enum HotkeyAction { Preset, Pause, Restore, Screenshot }
public sealed record HotkeyBinding(int Id, uint Modifiers, int Key, HotkeyAction Action, string? ProfileId = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Description => $"{Id}: {InputChord.Format(((Modifiers & 2) != 0 ? new[] { 17 } : []).Concat((Modifiers & 1) != 0 ? new[] { 18 } : []).Concat((Modifiers & 4) != 0 ? new[] { 16 } : []).Append(Key))} — {Action}";
}
public sealed record AutomationSettings
{
    public int SchemaVersion { get; init; } = 1;
    public bool AutomaticProfiles { get; init; }
    public bool HotkeysEnabled { get; init; }
    public bool MouseHolds { get; init; }
    public bool AcceptInjectedInput { get; init; }
    public ImmutableArray<int> HoldKeys { get; init; } = [0x75]; // F6, inactive by default.
    public HoldBoostSettings Boost { get; init; } = new();
    public DisplayProfile Desktop { get; init; } = new();
    public ImmutableArray<GameRule> Rules { get; init; } = [];
    public ImmutableArray<DisplayProfile> Profiles { get; init; } = [];
    public ImmutableArray<HotkeyBinding> Hotkeys { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public bool NeedsListener => AutomaticProfiles || HotkeysEnabled || Boost.Enabled;
}
