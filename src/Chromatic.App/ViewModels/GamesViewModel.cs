using System.Collections.Immutable;
using System.Windows.Input;
using Chromatic.Core.Models;
using Chromatic.Infrastructure.Discovery;
using Chromatic.Infrastructure.Persistence;
using Microsoft.Win32;

namespace Chromatic.App.ViewModels;

public sealed class GamesViewModel : PageViewModel
{
    private readonly GameLibraryRepository _repository;
    private readonly PresetsViewModel _presets;
    private readonly SemaphoreSlim _changes = new(1, 1);
    private GameLibrary _library = new();
    private bool _loaded;
    private SavedGame? _selected;
    private PresetDocument? _preset;
    private string _name = "My game", _path = "", _status = "Add an executable and choose a Color preset. Bookmarks do not switch colors automatically.";
    private IReadOnlyList<InstalledGame> _installed = [];
    private InstalledGame? _installedSelection;
    public override string CapabilityStatus => "Local game bookmarks · automatic switching and hotkeys unavailable";
    public ImmutableArray<SavedGame> Games => _library.Games;
    public IEnumerable<PresetDocument> ColorPresets => _presets.All.Where(p => p.Kind == PresetKind.Color);
    public IReadOnlyList<InstalledGame> Installed => _installed;
    public InstalledGame? InstalledSelection { get => _installedSelection; set { _installedSelection = value; Changed(); } }
    public SavedGame? Selected
    {
        get => _selected;
        set
        {
            _selected = value; Changed();
            if (value is not null)
            {
                EditName = value.Name; ExecutablePath = value.ExecutablePath;
                SelectedPreset = ColorPresets.FirstOrDefault(p => p.Id == value.PresetId);
                if (SelectedPreset is null) Status = "This bookmark references a removed preset. Choose a new Color preset and Save.";
            }
        }
    }
    public PresetDocument? SelectedPreset { get => _preset; set { _preset = value; Changed(); } }
    public string EditName { get => _name; set { _name = value; Changed(); } }
    public string ExecutablePath { get => _path; set { _path = value; Changed(); } }
    public string Status { get => _status; private set { _status = value; Changed(); } }
    public ICommand BrowseCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand DiscoverCommand { get; }
    public ICommand ChooseDiscoveredCommand { get; }
    public ICommand PreviewPresetCommand { get; }
    public GamesViewModel(GameLibraryRepository repository, PresetsViewModel presets) : base("Games", "Keep your game looks together.",
        "This checkpoint saves local executable/preset bookmarks. Automatic switching and input listeners are still under development. Discovery reads Steam library and manifest metadata only when requested; it never scans game files or launches games.")
    {
        _repository = repository; _presets = presets;
        _presets.LibraryChanged += () =>
        {
            var id = SelectedPreset?.Id; Changed(nameof(ColorPresets));
            SelectedPreset = ColorPresets.FirstOrDefault(p => p.Id == id);
            if (Selected is { } game && !ColorPresets.Any(p => p.Id == game.PresetId)) Status = "This bookmark references a removed preset. Choose a new Color preset and Save.";
        };
        AsyncCommand Run(Func<Task> action) => new(action, e => Status = $"Operation failed: {e.Message}");
        BrowseCommand = new RelayCommand(_ => PickExecutable(null));
        NewCommand = new RelayCommand(_ => { Selected = null; EditName = "My game"; ExecutablePath = ""; SelectedPreset = null; });
        SaveCommand = Run(SaveAsync); DeleteCommand = Run(DeleteAsync);
        DiscoverCommand = Run(async () =>
        {
            var root = SteamDiscovery.UserSteamPath();
            if (root is null) { Status = "Steam is not registered for this user. Use Browse executable instead."; return; }
            var games = await Task.Run(() => SteamDiscovery.Discover(root)); _installed = games; Changed(nameof(Installed));
            Status = $"Found {games.Count} Steam installations from manifests. Choose one, then pick its executable manually.";
        });
        ChooseDiscoveredCommand = new RelayCommand(_ =>
        {
            if (InstalledSelection is not { } game) { Status = "Select a discovered game first."; return; }
            Selected = null; EditName = game.Name; PickExecutable(game.InstallDirectory);
        });
        PreviewPresetCommand = new RelayCommand(_ =>
        {
            if (SelectedPreset is not { } preset) { Status = "Select an available Color preset first."; return; }
            _presets.Selected = preset; _presets.Preview(); Status = "Preset loaded into Display samples. Apply from Display or Library when ready.";
        });
    }
    private void PickExecutable(string? directory)
    {
        try
        {
            var dialog = new OpenFileDialog { Filter = "Windows executable|*.exe", CheckFileExists = true };
            if (directory is not null && System.IO.Directory.Exists(directory)) dialog.InitialDirectory = directory;
            if (dialog.ShowDialog() == true) ExecutablePath = SteamDiscovery.NormalizeExecutable(dialog.FileName);
        }
        catch (Exception e) { Status = $"Executable selection rejected: {e.Message}"; }
    }
    public async Task LoadAsync()
    {
        try { var loaded = await _repository.LoadAsync(); _library = loaded.Value; _loaded = true; Changed(nameof(Games)); Status = loaded.Warning ?? "Game bookmarks loaded. Automatic switching remains unavailable."; }
        catch (Exception e) { _loaded = false; Status = $"Games could not load: {e.Message} Files preserved; saving is disabled."; }
    }
    private async Task ChangeAsync(Func<GameLibrary, GameLibrary> change, string message, string? selectId = null)
    {
        await _changes.WaitAsync();
        try
        {
            if (!_loaded) throw new InvalidOperationException("Game bookmarks must load successfully before saving.");
            var next = change(_library);
            try { await _repository.SaveAsync(next); }
            catch
            {
                try { _library = (await _repository.LoadAsync()).Value; Changed(nameof(Games)); Selected = null; } catch { _loaded = false; }
                throw;
            }
            var id = selectId ?? Selected?.Id; _library = next; Changed(nameof(Games)); Selected = Games.FirstOrDefault(g => g.Id == id); Status = message;
        }
        finally { _changes.Release(); }
    }
    public Task SaveAsync()
    {
        var preset = SelectedPreset ?? throw new InvalidOperationException("Choose a Color preset.");
        if (!ColorPresets.Any(p => p.Id == preset.Id)) throw new InvalidOperationException("The selected preset no longer exists.");
        var game = new SavedGame(Selected?.Id ?? Guid.NewGuid().ToString("N"), EditName.Trim(), SteamDiscovery.NormalizeExecutable(ExecutablePath), preset.Id);
        return ChangeAsync(l => l with { Games = l.Games.Where(g => g.Id != game.Id).Append(game).ToImmutableArray() }, "Bookmark saved. Nothing was launched or applied.", game.Id);
    }
    public Task DeleteAsync()
    {
        var game = Selected ?? throw new InvalidOperationException("Select a saved game.");
        return ChangeAsync(l => l with { Games = l.Games.Remove(game) }, "Bookmark removed. Game files were not touched.");
    }
}
