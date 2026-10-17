using System.IO;
using Chromatic.App.ViewModels;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Persistence;
using Xunit;

namespace Chromatic.Ui.Tests;

public sealed class LibraryWorkflowTests
{
    [Fact] public async Task AccentDraftIsSavedSharedAndConfirmedWithItsWholeImageWarningWithoutApplying()
    {
        using var fixture = new Fixture(); await fixture.Library.LoadAsync(); fixture.Display.PrepareBlueHoloCommand.Execute(null);
        var colour = fixture.Display.DraftColor; fixture.Library.EditName = "My blue-accent test"; await fixture.Library.SaveNewAsync();
        Assert.Equal(colour, fixture.Library.Selected!.Color);
        fixture.Library.ShareCommand.Execute(null); fixture.Library.StageImportText(fixture.Library.ShareText);
        Assert.Contains("colour darkening", fixture.Library.ImportDiff); Assert.Contains("white remains white", fixture.Library.ImportDiff);
        await fixture.Library.ConfirmImportAsync(); Assert.Equal(colour, fixture.Library.Selected!.Color); Assert.Empty(fixture.Client.Commands);
    }
    [Fact] public async Task SiegeNamedStylesAreAddedToAnExistingPackAndPreviewWithoutApplyingOrReplacingEdits()
    {
        using var fixture = new Fixture(); var library = fixture.Library;
        var earlier = GamePresetCatalog.Presets.Where(p => p.Id is not "builtin-game-siege-champ" and not "builtin-game-siege-diamond1")
            .Select(p => p.Id == "builtin-game-siege-color" ? p with { Name = "My Strong color" } : p);
        await new LibraryRepository(fixture.LibraryPath).SaveAsync(new() { Presets = BuiltInPresets.Create().AddRange(earlier) });
        await library.LoadAsync(); Assert.Equal(19, library.All.Length);
        await library.AddGamePresetsAsync(); Assert.Equal(21, library.All.Length); Assert.Equal(6, library.Filtered.Count());
        Assert.Equal("My Strong color", library.All.Single(p => p.Id == "builtin-game-siege-color").Name);
        library.Search = "champ"; library.Selected = Assert.Single(library.Filtered); Assert.True(library.Preview());
        Assert.Equal(220, fixture.Display.Saturation); Assert.Equal(1, fixture.Display.Gamma);
        library.EditName = "My Champ"; await library.RenameAsync();
        await library.AddGamePresetsAsync(); Assert.Equal(21, library.All.Length);
        Assert.Equal("My Champ", library.All.Single(p => p.Id == "builtin-game-siege-champ").Name);
        library.Search = "im diamond 1"; library.Selected = Assert.Single(library.Filtered); Assert.True(library.Preview());
        Assert.Equal(190, fixture.Display.Saturation);
        await library.LoadAsync(); Assert.Equal(21, library.All.Length); Assert.Empty(fixture.Client.Commands);
    }
    [Fact] public async Task RustRefreshUpdatesStoredOldDefaultsWhileKeepingCustomizedEntriesAndTheSiegeFavorite()
    {
        using var fixture = new Fixture(); var library = fixture.Library;
        PresetDocument Old(string suffix, string name, ColorSettings color) => new()
        { Id = "builtin-game-rust-" + suffix, Name = name, GameTag = "Rust", Color = color };
        PresetDocument[] earlier = [
            Old("forest", "Rust — Forest / Temperate", new(135, Brightness: .015, Contrast: .96)) with { Name = "My forest" },
            Old("desert", "Rust — Desert / Arid", new(120, Brightness: -.01, Contrast: .96)),
            Old("snow", "Rust — Snow / Arctic", new(125, Brightness: -.015, Contrast: .94)),
            Old("tundra", "Rust — Tundra", new(145, Brightness: .005, Contrast: .98)),
            Old("jungle", "Rust — Jungle", new(125, Brightness: .03, Contrast: .92)) ];
        var previous = BuiltInPresets.Create().AddRange(GamePresetCatalog.Presets.Where(p => p.GameTag != GamePresetCatalog.Rust)).AddRange(earlier);
        await new LibraryRepository(fixture.LibraryPath).SaveAsync(new() { Presets = previous }); await library.LoadAsync();
        library.Selected = library.All.Single(p => p.Id.EndsWith("jungle")); Assert.Contains("Earlier Rust", library.SelectedDescription);
        fixture.Display.Saturation = 200; await library.AddRustPresetsAsync();
        Assert.Equal(21, library.All.Length); Assert.Equal(earlier[0], library.Selected); Assert.Equal(200, fixture.Display.Saturation);
        foreach (var old in earlier.Skip(1)) Assert.Equal(GamePresetCatalog.FindOriginal(old.Id), library.All.Single(p => p.Id == old.Id));
        Assert.Equal(previous.Single(p => p.Id == "builtin-game-siege-color"), library.All.Single(p => p.Id == "builtin-game-siege-color"));
        await library.LoadAsync(); await library.AddRustPresetsAsync();
        Assert.Equal(21, library.All.Length); Assert.Equal(earlier[0], library.All.Single(p => p.Id == earlier[0].Id));
        Assert.Empty(fixture.Client.Commands);
    }
    [Fact] public async Task AddingGamePackPersistsOncePerIdAndPreservesExistingEditsWithoutApplying()
    {
        using var fixture = new Fixture(); var library = fixture.Library; await library.LoadAsync();
        await library.AddGamePresetsAsync(); Assert.Equal(21, library.All.Length); Assert.Equal(6, library.Filtered.Count());
        Assert.Equal(GamePresetCatalog.Siege, library.GameFilter); Assert.Equal("builtin-game-siege-balanced", library.Selected!.Id);
        fixture.Display.Saturation = 177; await library.UpdateColorsAsync();
        library.EditName = "My tuned Siege look"; library.EditTag = "My game tag"; await library.RenameAsync();
        var edited = library.Selected!;
        await library.AddGamePresetsAsync(); Assert.Equal(21, library.All.Length);
        Assert.Equal(edited, library.All.Single(p => p.Id == edited.Id));
        Assert.Contains(library.Selected, library.Filtered);
        Assert.Empty(fixture.Client.Commands); Assert.Equal(177, fixture.Display.Saturation);
        await library.LoadAsync(); Assert.Equal(21, library.All.Length); Assert.Equal(edited, library.All.Single(p => p.Id == edited.Id));
        library.GameFilter = GamePresetCatalog.Siege; Assert.Equal(5, library.Filtered.Count());
        library.GameFilter = "Counter-Strike 2"; Assert.Single(library.Filtered);
    }
    [Fact] public async Task RustPackUpgradesAnExistingGameLibraryAndKeepsEditsWhilePreviewStaysLocal()
    {
        using var fixture = new Fixture(); var library = fixture.Library;
        var legacyGames = GamePresetCatalog.Presets.Where(p => p.GameTag != GamePresetCatalog.Rust);
        await new LibraryRepository(fixture.LibraryPath).SaveAsync(new() { Presets = BuiltInPresets.Create().AddRange(legacyGames) });
        await library.LoadAsync(); Assert.Equal(16, library.All.Length);
        await library.AddRustPresetsAsync(); Assert.Equal(21, library.All.Length);
        Assert.Equal(GamePresetCatalog.Rust, library.GameFilter); Assert.Equal(5, library.Filtered.Count());
        Assert.Equal("builtin-game-rust-forest", library.Selected!.Id);
        fixture.Display.Saturation = 177; library.EditName = "My Rust forest";
        await library.UpdateColorsAsync(); await library.RenameAsync(); var edited = library.Selected!;
        await library.AddRustPresetsAsync(); Assert.Equal(21, library.All.Length); Assert.Equal(edited, library.Selected);
        library.Selected = library.Filtered.Single(p => p.Id.EndsWith("snow")); Assert.True(library.Preview());
        Assert.Equal(library.Selected.Color, fixture.Display.DraftColor);
        await library.ResetSelectedAsync(); Assert.Equal(edited, library.All.Single(p => p.Id == edited.Id));
        await library.LoadAsync(); Assert.Equal(5, library.All.Count(p => p.GameTag == GamePresetCatalog.Rust));
        Assert.Empty(fixture.Client.Commands);
    }
    [Fact] public async Task ResetRestoresOnlyTheChosenStartingPointAndLeavesTheDisplayUntouched()
    {
        using var fixture = new Fixture(); var library = fixture.Library; await library.LoadAsync(); await library.AddGamePresetsAsync();
        var id = library.Selected!.Id; fixture.Display.Saturation = 180; await library.UpdateColorsAsync();
        library.EditName = "Keep this copy"; await library.SaveNewAsync(); var custom = library.Selected!;
        Assert.False(library.CanResetSelected);
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.ResetSelectedAsync());
        library.Selected = library.All.Single(p => p.Id == id); Assert.True(library.CanResetSelected);
        await library.ResetSelectedAsync();
        Assert.Equal(GamePresetCatalog.FindOriginal(id), library.Selected);
        Assert.Equal(custom, library.All.Single(p => p.Id == custom.Id));
        Assert.Equal(180, fixture.Display.Saturation); Assert.Empty(fixture.Client.Commands);
        Assert.DoesNotContain("Customized", library.SelectedDescription);
    }
    [Fact] public async Task InterruptedGamePackAdditionKeepsThePreviousLibraryAndNoHardwareCommands()
    {
        using var fixture = new Fixture(); await fixture.Library.LoadAsync();
        await new LibraryRepository(fixture.LibraryPath).SaveAsync(new() { Presets = fixture.Library.All });
        var library = new PresetsViewModel(new(fixture.LibraryPath, new Fault(WriteCheckpoint.BeforeReplace)), fixture.Display);
        await library.LoadAsync(); await Assert.ThrowsAsync<IOException>(() => library.AddGamePresetsAsync());
        Assert.Equal(7, library.All.Length); Assert.Equal(7, (await new LibraryRepository(fixture.LibraryPath).LoadAsync()).Value.Presets.Length);
        Assert.Empty(fixture.Client.Commands);
    }
    [Fact] public async Task SavingEditingAndDuplicatingUseTheDraftWithoutSendingHardwareCommands()
    {
        using var fixture = new Fixture(); var library = fixture.Library; await library.LoadAsync();
        fixture.Display.Saturation = 175; fixture.Display.OutputWarmth = .1;
        library.EditName = "Siege colors"; library.EditTag = "Rainbow Six Siege"; await library.SaveNewAsync();
        var original = library.Selected!; Assert.Equal(175, original.Color!.SaturationPercent); Assert.Equal(PresetKind.Color, original.Kind);
        fixture.Display.Saturation = 200; await library.UpdateColorsAsync();
        library.EditName = "Edited"; await library.RenameAsync();
        await library.DuplicateAsync(); Assert.NotEqual(original.Id, library.Selected!.Id);
        Assert.Equal(200, library.Selected.Color!.SaturationPercent); Assert.Empty(fixture.Client.Commands);
        library.Search = "edited"; Assert.Equal(2, library.Filtered.Count());
        library.GameFilter = "Rainbow Six Siege"; Assert.Equal(2, library.Filtered.Count());
        library.TypeFilter = "Resolution"; Assert.Empty(library.Filtered);
        await library.DeleteAsync(); await library.ResetBuiltInsAsync();
        Assert.Equal(8, library.All.Length); Assert.Equal(.1, fixture.Display.OutputWarmth);
        var reloaded = await new LibraryRepository(fixture.LibraryPath).LoadAsync(); Assert.Equal(8, reloaded.Value.Presets.Length);
    }
    [Fact] public async Task ImportsRequireConfirmationAndNonColorDocumentsCannotClaimApplySuccess()
    {
        using var fixture = new Fixture(); var library = fixture.Library; await library.LoadAsync();
        var imported = new PresetDocument { Id = "builtin-neutral", Name = "Pending mode", Kind = PresetKind.Resolution,
            Color = null, DisplayIntent = new(1280, 960, new(144, 1)) };
        library.StageImportText(PresetExchange.Share(imported));
        Assert.True(library.HasPendingImport); Assert.Equal(7, library.All.Length); Assert.False(File.Exists(fixture.LibraryPath));
        Assert.Contains("Stage 4", library.ImportDiff); Assert.Empty(fixture.Client.Commands);
        await library.ConfirmImportAsync(); Assert.False(library.HasPendingImport); Assert.Equal(8, library.All.Length);
        Assert.NotEqual("builtin-neutral", library.Selected!.Id); await library.ApplyAsync();
        Assert.Contains("await later stages", library.Status); Assert.Empty(fixture.Client.Commands);
        Assert.Equal(100, fixture.Display.Saturation);
        library.StageImport(new()); library.CancelImport();
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.ConfirmImportAsync());
        Assert.Equal(8, library.All.Length);
    }
    [Fact] public async Task InvalidImportClearsEarlierPendingConfirmation()
    {
        using var fixture = new Fixture(); await fixture.Library.LoadAsync(); fixture.Library.StageImport(new());
        Assert.Throws<ContractException>(() => fixture.Library.StageImportText("broken"));
        Assert.False(fixture.Library.HasPendingImport); Assert.Equal(7, fixture.Library.All.Length);
    }
    [Fact] public async Task PreviewDoesNotApplyAndExplicitApplyShowsActualFailure()
    {
        using var fixture = new Fixture(); await fixture.Library.LoadAsync();
        fixture.Library.Selected = fixture.Library.All.Single(p => p.Id == "builtin-treeline");
        fixture.Display.OutputWarmth = .1; Assert.True(fixture.Library.Preview());
        Assert.Equal(135, fixture.Display.Saturation); Assert.Equal(.1, fixture.Display.OutputWarmth); Assert.Empty(fixture.Client.Commands);
        fixture.Display.OutputWarmth = 0; await fixture.Library.ApplyAsync();
        Assert.IsType<ApplyProfile>(Assert.Single(fixture.Client.Commands)); Assert.Contains("InjectedFailure", fixture.Library.Status);
    }
    [Fact] public async Task CorruptLibraryIsPreservedAndEditingCannotOverwriteIt()
    {
        using var fixture = new Fixture(); await File.WriteAllTextAsync(fixture.LibraryPath, "broken");
        await fixture.Library.LoadAsync(); Assert.Contains("saving is disabled", fixture.Library.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Library.SaveNewAsync());
        Assert.Equal("broken", await File.ReadAllTextAsync(fixture.LibraryPath)); Assert.Empty(fixture.Client.Commands);
    }
    [Theory] [InlineData(WriteCheckpoint.BeforeReplace, 7)] [InlineData(WriteCheckpoint.AfterReplace, 8)]
    public async Task FailedSaveReloadsDiskStateInsteadOfClaimingAnAssumedRollback(WriteCheckpoint point, int expected)
    {
        using var fixture = new Fixture(); await fixture.Library.LoadAsync();
        await new LibraryRepository(fixture.LibraryPath).SaveAsync(new() { Presets = fixture.Library.All });
        var vm = new PresetsViewModel(new(fixture.LibraryPath, new Fault(point)), fixture.Display);
        await vm.LoadAsync(); await Assert.ThrowsAsync<IOException>(() => vm.SaveNewAsync());
        Assert.Equal(expected, vm.All.Length); Assert.Equal(expected, (await new LibraryRepository(fixture.LibraryPath).LoadAsync()).Value.Presets.Length);
    }
    [Fact] public async Task BookmarksSaveEditRemoveAndDetectDeletedPresetWithoutOpeningGames()
    {
        using var fixture = new Fixture(); await fixture.Library.LoadAsync();
        var games = new GamesViewModel(new(Path.Combine(fixture.Root, "games.json")), fixture.Library, new(new(Path.Combine(fixture.Root, "automation.json")), fixture.Library, fixture.Display, fixture.Client)); await games.LoadAsync();
        games.EditName = "Siege"; games.ExecutablePath = @"C:\Games\Siege.exe"; games.SelectedPreset = fixture.Library.All[0];
        await games.SaveAsync(); Assert.Single(games.Games); Assert.NotNull(games.Selected);
        games.EditName = "Siege fullscreen"; await games.SaveAsync(); Assert.Single(games.Games);
        Assert.Equal("Siege fullscreen", games.Games[0].Name);
        await fixture.Library.DeleteAsync(); Assert.Null(games.SelectedPreset); Assert.Contains("removed preset", games.Status);
        games.Selected = null; games.Selected = games.Games[0]; Assert.Contains("removed preset", games.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => games.SaveAsync());
        await games.DeleteAsync(); Assert.Empty(games.Games); Assert.Empty(fixture.Client.Commands);
    }
    private sealed class Fault(WriteCheckpoint at) : IWriteFaultInjector
    { public void Check(WriteCheckpoint checkpoint) { if (checkpoint == at) throw new IOException("Injected fault."); } }
    private sealed class FakeClient : IEffectsClient
    {
        public List<EffectsCommand> Commands { get; } = [];
        public EffectsState State { get; } = new() { Enabled = true, Held = true, HoldBoost = new() { Enabled = true },
            Capabilities = new([new(BackendKind.Magnification, Capability.Available, "Injected", EffectScope.GlobalDesktop)]) };
        public Task<EffectsAcknowledgment> SendAsync(CommandEnvelope command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.Command); var reduced = EffectsReducer.Reduce(State, command);
            return Task.FromResult(new EffectsAcknowledgment(reduced.Result, reduced.State, new(false, "Fake", "InjectedFailure", "No native calls.", reduced.State.Revision)));
        }
    }
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "Chromatic.LibraryTests", Guid.NewGuid().ToString("N"));
        public string LibraryPath => Path.Combine(Root, "library.json");
        public FakeClient Client { get; } = new();
        public DisplayViewModel Display { get; }
        public PresetsViewModel Library { get; }
        public Fixture() { Directory.CreateDirectory(Root); Display = new(Client); Library = new(new(LibraryPath), Display); }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
