using System.Collections.Immutable;

namespace Chromatic.Core.Models;

public sealed record PresetLibrary
{
    public int SchemaVersion { get; init; } = 1;
    public ImmutableArray<PresetDocument> Presets { get; init; } = [];
}

// Local bookmarks only. No process listener or automatic activation is implied.
public sealed record SavedGame(string Id, string Name, string ExecutablePath, string PresetId);
public sealed record GameLibrary
{
    public int SchemaVersion { get; init; } = 1;
    public ImmutableArray<SavedGame> Games { get; init; } = [];
}
