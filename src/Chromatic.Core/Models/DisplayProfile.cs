using System.Collections.Immutable;

namespace Chromatic.Core.Models;

public sealed record DisplayProfile
{
    public int SchemaVersion { get; init; } = 1;
    public string Id { get; init; } = "desktop";
    public string Name { get; init; } = "Desktop";
    public ColorSettings GlobalColor { get; init; } = new();
    public ImmutableArray<MonitorColorSettings> Monitors { get; init; } = [];
    public CrosshairSettings Crosshair { get; init; } = new();
    public DisplayIntent? DisplayIntent { get; init; }
}
