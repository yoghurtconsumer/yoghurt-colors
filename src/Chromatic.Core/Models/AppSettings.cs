using System.Collections.Immutable;

namespace Chromatic.Core.Models;

public enum CloseBehavior { ExitAndRestore, MinimizeToTray }
public enum ThemeKind { Default, Slate }
public sealed record OptionalServices(bool Phone = false, bool Startup = false,
    bool Discord = false, bool Cloud = false, bool Updates = false, bool GammaCompatibility = false,
    bool NvidiaExperimental = false)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool DisplayPreviewExperimental { get; init; }
}

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public bool EffectsEnabled { get; init; }
    public ColorSettings GlobalColor { get; init; } = new();
    public ImmutableArray<MonitorColorSettings> Monitors { get; init; } = [];
    public CrosshairSettings Crosshair { get; init; } = new();
    public HoldBoostSettings HoldBoost { get; init; } = new();
    public OptionalServices Services { get; init; } = new();
    public CloseBehavior CloseBehavior { get; init; } = CloseBehavior.ExitAndRestore;
    public ThemeKind Theme { get; init; }
}
