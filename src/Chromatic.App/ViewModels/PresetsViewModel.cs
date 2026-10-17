using System.Collections.Immutable;
using System.IO;
using System.Windows.Input;
using Chromatic.Core.Models;
using Chromatic.Infrastructure.Persistence;
using Microsoft.Win32;

namespace Chromatic.App.ViewModels;

public sealed class PresetsViewModel : PageViewModel
{
    private readonly LibraryRepository _repository;
    private readonly DisplayViewModel _display;
    private readonly SemaphoreSlim _changes = new(1, 1);
    private PresetLibrary _library = new() { Presets = BuiltInPresets.Create() };
    private bool _loaded;
    private PresetDocument? _selected;
    private PresetDocument? _pending;
    private string _search = "", _type = "All", _game = "All", _name = "My colors", _tag = "", _share = "";
    private string _status = "Choose a preset to preview. Nothing is applied automatically.";
    public event Action? LibraryChanged;
    public override string CapabilityStatus => "Local library ready · desktop changes require explicit Apply";
    public ImmutableArray<PresetDocument> All => _library.Presets;
    public IEnumerable<PresetDocument> Filtered => All.Where(p =>
        (_type == "All" || p.Kind.ToString() == _type) && (_game == "All" || p.GameTag == _game) &&
        string.Join(" ", p.Name, p.GameTag, p.AuthorMetadata?.Name).Contains(_search, StringComparison.OrdinalIgnoreCase));
    public string[] Types { get; } = ["All", "Color", "Crosshair", "Resolution", "FullSetup"];
    public IEnumerable<string> GameTags => new[] { "All" }.Concat(All.Select(p => p.GameTag).OfType<string>().Distinct().Order());
    public string Search { get => _search; set { _search = value; Changed(); Changed(nameof(Filtered)); } }
    public string TypeFilter { get => _type; set { _type = value; Changed(); Changed(nameof(Filtered)); } }
    public string GameFilter { get => _game; set { _game = value; Changed(); Changed(nameof(Filtered)); } }
    public PresetDocument? Selected
    {
        get => _selected;
        set
        {
            _selected = value; Changed(); Changed(nameof(SelectedDescription)); Changed(nameof(CanResetSelected));
            if (value is not null) { EditName = value.Name; EditTag = value.GameTag ?? ""; }
        }
    }
    public string SelectedDescription => GamePresetCatalog.Describe(Selected);
    public bool CanResetSelected => Selected is { } preset && GamePresetCatalog.FindOriginal(preset.Id) is not null;
    public string EditName { get => _name; set { _name = value; Changed(); } }
    public string EditTag { get => _tag; set { _tag = value; Changed(); } }
    public string ShareText { get => _share; set { _share = value; Changed(); } }
    public string Status { get => _status; private set { _status = value; Changed(); } }
    public bool HasPendingImport => _pending is not null;
    public string ImportDiff => _pending is null ? "No pending import." : PresetExchange.Diff(_pending, _display.DraftColor);
    public ICommand SaveNewCommand { get; }
    public ICommand UpdateColorsCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ResetBuiltInsCommand { get; }
    public ICommand AddGamePresetsCommand { get; }
    public ICommand AddRustPresetsCommand { get; }
    public ICommand ResetSelectedCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand ImportFileCommand { get; }
    public ICommand ImportCodeCommand { get; }
    public ICommand ConfirmImportCommand { get; }
    public ICommand CancelImportCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ShareCommand { get; }
    public PresetsViewModel(LibraryRepository repository, DisplayViewModel display) : base("Presets / Library", "Save a look. Preview it. Share it offline.",
        "Color presets contain global color and tonal intentions. Per-monitor settings stay separate. Resolution/full-setup and crosshair documents can be stored and shared; application awaits their later stages.")
    {
        _repository = repository; _display = display; _selected = All[0];
        AsyncCommand Run(Func<Task> action) => new(action, e => Status = $"Operation failed: {e.Message}");
        SaveNewCommand = Run(SaveNewAsync); UpdateColorsCommand = Run(UpdateColorsAsync); RenameCommand = Run(RenameAsync);
        DuplicateCommand = Run(DuplicateAsync); DeleteCommand = Run(DeleteAsync); ResetBuiltInsCommand = Run(ResetBuiltInsAsync);
        AddGamePresetsCommand = Run(AddGamePresetsAsync); ResetSelectedCommand = Run(ResetSelectedAsync);
        AddRustPresetsCommand = Run(AddRustPresetsAsync);
        PreviewCommand = new RelayCommand(_ => Preview()); ApplyCommand = Run(ApplyAsync);
        ImportFileCommand = Run(async () =>
        {
            var dialog = new OpenFileDialog { Filter = "Chromatic preset|*.chromaticpreset;*.json", CheckFileExists = true };
            if (dialog.ShowDialog() == true) { CancelImport(); StageImport(await PresetExchange.ReadFileAsync(dialog.FileName)); }
        });
        ImportCodeCommand = new RelayCommand(_ =>
        { try { StageImportText(ShareText.Trim()); } catch (Exception e) { CancelImport(); Status = $"Import rejected: {e.Message}"; } });
        ConfirmImportCommand = Run(ConfirmImportAsync); CancelImportCommand = new RelayCommand(_ => CancelImport());
        ExportCommand = Run(async () =>
        {
            var selected = RequireSelection(); _ = PresetExchange.Encode(selected);
            var dialog = new SaveFileDialog { Filter = "Chromatic preset|*.chromaticpreset|JSON|*.json", DefaultExt = ".chromaticpreset", FileName = "chromatic-preset.chromaticpreset", AddExtension = true };
            if (dialog.ShowDialog() == true) { await PresetExchange.WriteFileAsync(dialog.FileName, selected); Status = "Preset exported atomically. No desktop settings changed."; }
        });
        ShareCommand = new RelayCommand(_ =>
        { try { ShareText = PresetExchange.Share(RequireSelection()); Status = "Offline share code generated. Copy it from the text box; checksum detects corruption, not a trusted author."; } catch (Exception e) { Status = e.Message; } });
    }
    public async Task LoadAsync()
    {
        try
        {
            var loaded = await _repository.LoadAsync(); _library = loaded.Value; _loaded = true; Publish();
            Status = loaded.Warning ?? "Library loaded. Preview edits the samples; Apply requests desktop changes.";
        }
        catch (Exception e) { _loaded = false; Status = $"Library could not load: {e.Message} Files preserved; saving is disabled."; }
    }
    private PresetDocument RequireSelection() => Selected ?? throw new InvalidOperationException("Select a preset first.");
    private static PresetDocument LocalCopy(PresetDocument preset) => preset with { Id = Guid.NewGuid().ToString("N") };
    private string? Tag => string.IsNullOrWhiteSpace(EditTag) ? null : EditTag.Trim();
    private async Task ChangeAsync(Func<PresetLibrary, PresetLibrary> change, string message, string? selectId = null)
    {
        await _changes.WaitAsync();
        try
        {
            if (!_loaded) throw new InvalidOperationException("Library must load successfully before saving.");
            var next = change(_library); LibraryRepository.Validate(next);
            try { await _repository.SaveAsync(next); }
            catch
            {
                // A failure after replacement may have committed; reload authoritative data.
                try { _library = (await _repository.LoadAsync()).Value; Publish(); } catch { _loaded = false; }
                throw;
            }
            _library = next; Publish(selectId); Status = message;
        }
        finally { _changes.Release(); }
    }
    private void Publish(string? selectId = null)
    {
        var id = selectId ?? Selected?.Id;
        Changed(nameof(All)); Changed(nameof(Filtered)); Changed(nameof(GameTags));
        Selected = All.FirstOrDefault(p => p.Id == id) ?? All.FirstOrDefault(); LibraryChanged?.Invoke();
    }
    public Task SaveNewAsync()
    {
        var preset = new PresetDocument { Id = Guid.NewGuid().ToString("N"), Name = EditName.Trim(), GameTag = Tag, Color = _display.DraftColor };
        return ChangeAsync(l => l with { Presets = l.Presets.Add(preset) }, "Saved the base color draft. Held boost and per-monitor settings were not included.", preset.Id);
    }
    public Task RenameAsync()
    {
        var id = RequireSelection().Id; var name = EditName.Trim(); var tag = Tag;
        return ChangeAsync(l => Edit(l, id, p => p with { Name = name, GameTag = tag }), "Preset details saved.", id);
    }
    public Task UpdateColorsAsync()
    {
        var old = RequireSelection();
        if (old.Kind != PresetKind.Color) throw new InvalidOperationException("Only Color presets can be updated from Display.");
        var color = _display.DraftColor;
        return ChangeAsync(l => Edit(l, old.Id, p => p with { Color = color }), "Preset updated from the base color draft.", old.Id);
    }
    public Task DuplicateAsync()
    {
        var copy = LocalCopy(RequireSelection());
        copy = copy with { Name = copy.Name.Length <= 121 ? copy.Name + " (copy)" : copy.Name[..121] + " (copy)" };
        return ChangeAsync(l => l with { Presets = l.Presets.Add(copy) }, "Local copy saved.", copy.Id);
    }
    public Task DeleteAsync()
    {
        var selected = RequireSelection();
        return ChangeAsync(l => l with { Presets = l.Presets.Where(p => p.Id != selected.Id).ToImmutableArray() }, "Preset deleted. Game bookmarks referencing it must be reassigned.");
    }
    private static PresetLibrary Edit(PresetLibrary library, string id, Func<PresetDocument, PresetDocument> edit)
    {
        var current = library.Presets.FirstOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("Preset was removed; select another preset.");
        return library with { Presets = library.Presets.Replace(current, edit(current)) };
    }
    public Task ResetBuiltInsAsync() => ChangeAsync(l => l with
    {
        Presets = l.Presets.Where(p => !BuiltInPresets.Create().Any(b => b.Id == p.Id)).Concat(BuiltInPresets.Create()).ToImmutableArray()
    }, "Original starting points reset; custom presets preserved.");
    public Task AddGamePresetsAsync() => AddStartingPointsAsync(GamePresetCatalog.Presets, GamePresetCatalog.Siege,
        "builtin-game-siege-balanced", "Game preset pack ready; existing presets and edits were kept. Choose Game tag for Siege, CS2, VALORANT, Apex or Rust. Preview first; Apply remains manual.");
    public Task AddRustPresetsAsync() => AddStartingPointsAsync(
        GamePresetCatalog.Presets.Where(p => p.GameTag == GamePresetCatalog.Rust).ToImmutableArray(), GamePresetCatalog.Rust,
        "builtin-game-rust-forest", "Rust presets ready: untouched earlier defaults updated for richer color and deeper blacks; customized presets kept. Preview, then Apply manually.", refreshRust: true);
    private async Task AddStartingPointsAsync(ImmutableArray<PresetDocument> presets, string game, string preferredId, string message, bool refreshRust = false)
    {
        await ChangeAsync(l =>
        {
            if (refreshRust) l = l with { Presets = l.Presets.Select(GamePresetCatalog.RefreshUneditedRustPreset).ToImmutableArray() };
            var existing = l.Presets.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
            var missing = presets.Where(p => !existing.Contains(p.Id)).ToArray();
            return missing.Length == 0 ? l : l with { Presets = l.Presets.AddRange(missing) };
        }, message);
        Search = ""; TypeFilter = "Color"; GameFilter = game;
        if (!Filtered.Any()) GameFilter = "All";
        Selected = Filtered.FirstOrDefault(p => p.Id == preferredId) ?? Filtered.FirstOrDefault();
    }
    public Task ResetSelectedAsync()
    {
        var selected = RequireSelection();
        var original = GamePresetCatalog.FindOriginal(selected.Id) ?? throw new InvalidOperationException("This is a custom preset; there is no bundled starting point to restore.");
        return ChangeAsync(l => Edit(l, selected.Id, _ => original), "Selected starting point reset in the library. Nothing applied to the desktop.", selected.Id);
    }
    public bool Preview()
    {
        if (Selected is not { Kind: PresetKind.Color, Color: { } color })
        { Status = "Select a Color preset. Crosshair, resolution and full-setup application await later stages; nothing applied."; return false; }
        _display.LoadColorDraft(color); Status = "Loaded into Display samples. No desktop changes made."; return true;
    }
    public async Task ApplyAsync()
    {
        if (!Preview()) return;
        await _display.ApplyAsync(); Status = _display.ApplyStatus;
    }
    public void StageImportText(string text)
    {
        CancelImport();
        StageImport(text.StartsWith("chromaticdesk:", StringComparison.OrdinalIgnoreCase) ? PresetExchange.ParseLink(text) : PresetExchange.ImportCode(text));
    }
    public void StageImport(PresetDocument preset)
    {
        CancelImport(); _pending = PresetExchange.Decode(PresetExchange.Encode(preset));
        Changed(nameof(HasPendingImport)); Changed(nameof(ImportDiff)); Status = "Review the import diff, then Confirm import to add a local copy.";
    }
    public async Task ConfirmImportAsync()
    {
        var pending = _pending ?? throw new InvalidOperationException("No validated import is awaiting confirmation.");
        var copy = LocalCopy(pending);
        await ChangeAsync(l => l with { Presets = l.Presets.Add(copy) }, "Import confirmed and saved as a local copy. Nothing applied to the desktop.", copy.Id);
        if (ReferenceEquals(_pending, pending)) CancelImport();
    }
    public void CancelImport() { _pending = null; Changed(nameof(HasPendingImport)); Changed(nameof(ImportDiff)); }
}
