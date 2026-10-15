using System.Collections.Immutable;

namespace Chromatic.Core.Models;

public enum PresetKind { Color, Crosshair, Resolution, FullSetup }
public sealed record AuthorMetadata(string Name, DateTimeOffset CreatedUtc);
public sealed record PresetDocument
{
    public int SchemaVersion { get; init; } = 1;
    public string Id { get; init; } = "local";
    public string Name { get; init; } = "New preset";
    public PresetKind Kind { get; init; }
    public ColorSettings? Color { get; init; } = new();
    public CrosshairSettings? Crosshair { get; init; }
    public DisplayIntent? DisplayIntent { get; init; }
    public string? GameTag { get; init; }
    public AuthorMetadata? AuthorMetadata { get; init; }
    public ImmutableArray<string> Assets { get; init; } = [];
}
