using System.Text.Json;
using Chromatic.Core.Models;
using Chromatic.Core.Validation;
using Chromatic.Infrastructure.Persistence;
using Xunit;
namespace Chromatic.Integration.Tests;

public sealed class PersistenceTests
{
    private sealed class FailAt(WriteCheckpoint point) : IWriteFaultInjector
    { public void Check(WriteCheckpoint checkpoint) { if (checkpoint == point) throw new IOException("Injected disk failure"); } }
    private static AtomicJsonStore<AppSettings> Store(string path, IWriteFaultInjector? faults = null) =>
        new(path, "settings", SettingsValidator.Validate, () => new(), faults);

    [Fact] public async Task FirstRunReturnsSafeDefaultsWithoutCreatingSettings()
    {
        using var folder = new TestDirectory();
        var result = await Store(folder.File()).LoadAsync();
        Assert.Equal(LoadOrigin.Default, result.Origin); Assert.False(result.Value.EffectsEnabled);
        Assert.False(File.Exists(folder.File()));
    }
    [Fact] public async Task AtomicReplacementKeepsPreviousValidatedDocument()
    {
        using var folder = new TestDirectory(); var store = Store(folder.File());
        await store.SaveAsync(new()); await store.SaveAsync(new() { GlobalColor = new(150) });
        Assert.Equal(150, (await store.LoadAsync()).Value.GlobalColor.SaturationPercent);
        var previous = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(folder.File() + ".last-good"), JsonContract.Options);
        Assert.Equal(100, previous!.GlobalColor.SaturationPercent);
        Assert.Empty(Directory.GetFiles(folder.Root, "*.tmp"));
    }
    [Theory] [InlineData(WriteCheckpoint.BeforeFlush)] [InlineData(WriteCheckpoint.BeforeReplace)]
    public async Task InterruptedWritePreservesPrimaryAndBackup(WriteCheckpoint point)
    {
        using var folder = new TestDirectory(); var store = Store(folder.File());
        await store.SaveAsync(new()); await store.SaveAsync(new() { GlobalColor = new(120) });
        var primary = await File.ReadAllBytesAsync(folder.File()); var backup = await File.ReadAllBytesAsync(folder.File() + ".last-good");
        await Assert.ThrowsAsync<IOException>(() => Store(folder.File(), new FailAt(point)).SaveAsync(new() { GlobalColor = new(300) }));
        Assert.Equal(primary, await File.ReadAllBytesAsync(folder.File())); Assert.Equal(backup, await File.ReadAllBytesAsync(folder.File() + ".last-good"));
        Assert.Empty(Directory.GetFiles(folder.Root, "*.tmp"));
    }
    [Fact] public async Task FailureAfterCommitIsResolvedByReadingAuthoritativeData()
    {
        using var folder = new TestDirectory(); await Store(folder.File()).SaveAsync(new());
        await Assert.ThrowsAsync<IOException>(() => Store(folder.File(), new FailAt(WriteCheckpoint.AfterReplace)).SaveAsync(new() { GlobalColor = new(145) }));
        Assert.Equal(145, (await Store(folder.File()).LoadAsync()).Value.GlobalColor.SaturationPercent);
    }
    [Theory] [InlineData("{broken")] [InlineData("{\"schemaVersion\":99}")]
    public async Task CorruptPrimaryLoadsLastGoodAndNeverRotatesCorruption(string corrupt)
    {
        using var folder = new TestDirectory(); var store = Store(folder.File());
        await store.SaveAsync(new()); await store.SaveAsync(new() { GlobalColor = new(160) });
        await File.WriteAllTextAsync(folder.File(), corrupt);
        var recovered = await store.LoadAsync();
        Assert.Equal(LoadOrigin.LastGood, recovered.Origin); Assert.NotNull(recovered.Warning);
        Assert.Equal(100, recovered.Value.GlobalColor.SaturationPercent);
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new()));
        Assert.Equal(corrupt, await File.ReadAllTextAsync(folder.File()));
        Assert.Equal(100, JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(folder.File() + ".last-good"), JsonContract.Options)!.GlobalColor.SaturationPercent);
    }
    [Fact] public async Task BothCorruptFilesFailExplicitlyWithoutResettingData()
    {
        using var folder = new TestDirectory();
        await File.WriteAllTextAsync(folder.File(), "broken"); await File.WriteAllTextAsync(folder.File() + ".last-good", "also broken");
        await Assert.ThrowsAsync<ContractException>(() => Store(folder.File()).LoadAsync());
        Assert.Equal("broken", await File.ReadAllTextAsync(folder.File()));
    }
    [Fact] public async Task OversizedFileIsRejectedBeforeDeserialization()
    {
        using var folder = new TestDirectory();
        await File.WriteAllTextAsync(folder.File(), new string(' ', 1_048_577));
        await Assert.ThrowsAsync<ContractException>(() => Store(folder.File()).LoadAsync());
    }
    [Fact] public async Task InvalidValueNeverTouchesExistingFile()
    {
        using var folder = new TestDirectory(); var store = Store(folder.File()); await store.SaveAsync(new());
        var bytes = await File.ReadAllBytesAsync(folder.File());
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(new() { GlobalColor = new(double.NaN) }));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(folder.File()));
    }
    [Fact] public async Task ConcurrentRepositoryInstancesSerializeWrites()
    {
        using var folder = new TestDirectory();
        await Task.WhenAll(Enumerable.Range(100, 20).Select(i => Store(folder.File()).SaveAsync(new() { GlobalColor = new(i) })));
        var value = (await Store(folder.File()).LoadAsync()).Value.GlobalColor.SaturationPercent;
        Assert.InRange(value, 100, 119); Assert.True(File.Exists(folder.File() + ".last-good"));
    }
    [Fact] public async Task CrossProcessLeaseContentionDoesNotChangeData()
    {
        using var folder = new TestDirectory(); await Store(folder.File()).SaveAsync(new());
        await using var heldLease = new FileStream(folder.File() + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        await Assert.ThrowsAsync<IOException>(() => Store(folder.File()).SaveAsync(new() { GlobalColor = new(180) }));
        Assert.Equal(100, (await Store(folder.File()).LoadAsync()).Value.GlobalColor.SaturationPercent);
    }
    [Fact] public async Task ProfileLookupCannotEscapeItsDirectory()
    {
        using var folder = new TestDirectory(); var repository = new ProfileRepository(folder.Root);
        var profile = new DisplayProfile { Id = "../../outside", Name = "Portable identifier" };
        await repository.SaveAsync(profile);
        Assert.Equal(profile.Id, (await repository.LoadAsync(profile.Id))!.Id);
        Assert.Single(Directory.GetFiles(folder.Root, "*.json"));
    }
}
