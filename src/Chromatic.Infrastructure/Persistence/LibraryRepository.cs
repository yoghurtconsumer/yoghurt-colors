using System.Collections.Immutable;
using Chromatic.Core.Models;
using Chromatic.Core.Validation;
namespace Chromatic.Infrastructure.Persistence;

public sealed class LibraryRepository(string path, IWriteFaultInjector? faults = null)
{
    private readonly AtomicJsonStore<PresetLibrary> _store = new(path, "library", Validate, () => new() { Presets = BuiltInPresets.Create() }, faults);
    public Task<LoadResult<PresetLibrary>> LoadAsync(CancellationToken token = default) => _store.LoadAsync(token);
    public Task SaveAsync(PresetLibrary library, CancellationToken token = default) => _store.SaveAsync(library, token);
    public static void Validate(PresetLibrary library)
    {
        if (library.SchemaVersion != 1 || library.Presets.IsDefault || library.Presets.Length > 256)
            throw new ArgumentException("Invalid library version/size.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var preset in library.Presets) { PresetValidator.Validate(preset); if (!ids.Add(preset.Id)) throw new ArgumentException("Duplicate preset ID."); }
    }
}
public sealed class GameLibraryRepository(string path)
{
    private readonly AtomicJsonStore<GameLibrary> _store = new(path, "games", Validate, () => new());
    public Task<LoadResult<GameLibrary>> LoadAsync(CancellationToken token = default) => _store.LoadAsync(token);
    public Task SaveAsync(GameLibrary games, CancellationToken token = default) => _store.SaveAsync(games, token);
    public static void Validate(GameLibrary library)
    {
        if (library.SchemaVersion != 1 || library.Games.IsDefault || library.Games.Length > 256)
            throw new ArgumentException("Invalid game library version/size.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in library.Games)
        {
            SettingsValidator.Text(game.Id, 128, "game ID");
            SettingsValidator.Text(game.Name, 128, "game name");
            SettingsValidator.Text(game.PresetId, 128, "preset ID");
            var normalized = Discovery.SteamDiscovery.NormalizeExecutable(game.ExecutablePath);
            if (normalized != game.ExecutablePath || !ids.Add(game.Id) || !paths.Add(normalized))
                throw new ArgumentException("Games need unique IDs and canonical local executable paths.");
        }
    }
}
