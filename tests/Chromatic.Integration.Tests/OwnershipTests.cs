using System.Collections.Immutable;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Chromatic.Infrastructure.Effects;
using Chromatic.Infrastructure.Persistence;
using Chromatic.Recovery;
using Xunit;

namespace Chromatic.Integration.Tests;
public sealed class OwnershipTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task RecoveryRefusesOlderLastGoodJournalWhenPrimaryIsCorruptOrMissing(bool missing)
    {
        using var directory = new TestDirectory(); var path = directory.File();
        var store = new RecoveryJournalStore(path);
        await store.SaveAsync(Identity(), default);
        await store.SaveAsync(Identity() with { Status = JournalStatus.Applied }, default);
        if (missing) File.Delete(path); else await File.WriteAllTextAsync(path, "{corrupt");
        await Assert.ThrowsAsync<ContractException>(() => store.LoadAsync(default));
        Assert.True(File.Exists(path + ".last-good"));
        if (!missing) Assert.Equal("{corrupt", await File.ReadAllTextAsync(path));
    }
    private static ResourceValue Value(double saturation) => new(ColorMatrixBuilder.Saturation(saturation).Values);
    private static RecoverySnapshot Identity() => new() { UserSid = "S-1-5-21-1", SessionId = 1, AgentPid = 123, AgentStartTicks = 100 };
    private sealed class Gate : IWriteSafetyGate
    {
        public bool RecoveryReady { get; set; } = true;
        public WriteFence Current { get; set; } = new(1, 1);
        public bool CanWrite(WriteFence fence) => fence == Current;
    }
    private sealed class Journal : IRecoveryJournalStore
    {
        public RecoverySnapshot? Saved;
        public bool Fail;
        public Action? AfterSave;
        public Task<RecoverySnapshot?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Saved);
        public Task SaveAsync(RecoverySnapshot snapshot, CancellationToken cancellationToken)
        {
            if (Fail) throw new IOException("Injected journal flush failure.");
            Saved = snapshot; AfterSave?.Invoke(); return Task.CompletedTask;
        }
    }
    private sealed class Resource(ResourceValue initial) : IResourceAdapter
    {
        public ResourceValue Current = initial;
        public int Writes;
        public bool RejectOwnership, FailAfterWrite, IgnoreWrite;
        public Action? BeforeWrite;
        public ResourceDescriptor Descriptor { get; } = new(OwnedResourceKind.GlobalMatrix, EffectScope.GlobalDesktop);
        public ResourceValue Canonicalize(ResourceValue value) => value;
        public bool CanOwn(ResourceValue baseline) => !RejectOwnership;
        public Task<ResourceValue> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Current);
        public Task WriteAsync(ResourceValue value, CancellationToken cancellationToken)
        {
            BeforeWrite?.Invoke(); Writes++;
            if (!IgnoreWrite) Current = value;
            if (FailAfterWrite) throw new IOException("Injected crash after setter.");
            return Task.CompletedTask;
        }
    }
    [Fact] public async Task JournalsPendingWriteBeforeSetterAndRestoresActualNonIdentityBaseline()
    {
        var journal = new Journal(); var gate = new Gate(); var original = Value(115); var resource = new Resource(original);
        resource.BeforeWrite = () => Assert.NotNull(journal.Saved?.Resources[0].PendingValue);
        using var controller = new OwnedResourceController(journal, Identity(), gate, () => true);
        Assert.True((await controller.ApplyAsync(resource, Value(180), gate.Current, default)).Success);
        Assert.Null(journal.Saved!.Resources[0].PendingValue);
        Assert.True(OwnedResourceController.Equivalent(original, journal.Saved.Resources[0].Baseline));
        resource.BeforeWrite = null;
        Assert.True((await controller.RestoreAsync([resource], 2, default)).Success);
        Assert.True(OwnedResourceController.Equivalent(original, resource.Current));
        Assert.Equal(JournalStatus.Restored, journal.Saved.Status);
    }
    [Fact] public async Task JournalFailurePreventsNativeWrite()
    {
        var journal = new Journal { Fail = true }; var gate = new Gate(); var resource = new Resource(Value(100));
        using var controller = new OwnedResourceController(journal, Identity(), gate, () => true);
        Assert.False((await controller.ApplyAsync(resource, Value(200), gate.Current, default)).Success);
        Assert.Equal(0, resource.Writes);
    }
    [Fact] public async Task RechecksGenerationAfterSlowJournalFlush()
    {
        var journal = new Journal(); var gate = new Gate(); var resource = new Resource(Value(100));
        journal.AfterSave = () => gate.Current = new(2, 2);
        using var controller = new OwnedResourceController(journal, Identity(), gate, () => true);
        var result = await controller.ApplyAsync(resource, Value(200), new(1, 1), default);
        Assert.Equal("StaleRevision", result.ErrorCode); Assert.Equal(0, resource.Writes);
    }
    [Fact] public async Task RecoveryAndLeaseMustRemainAvailableUntilWrite()
    {
        var journal = new Journal(); var gate = new Gate(); var resource = new Resource(Value(100)); var lease = true;
        journal.AfterSave = () => lease = false;
        using var controller = new OwnedResourceController(journal, Identity(), gate, () => lease);
        Assert.False((await controller.ApplyAsync(resource, Value(200), gate.Current, default)).Success);
        Assert.Equal(0, resource.Writes);
        gate.RecoveryReady = false;
        Assert.False((await controller.ApplyAsync(resource, Value(200), gate.Current, default)).Success);
    }
    [Fact] public async Task ForeignWriterIsNotOverwrittenByApplyOrRestore()
    {
        var journal = new Journal(); var gate = new Gate(); var resource = new Resource(Value(100));
        using var controller = new OwnedResourceController(journal, Identity(), gate, () => true);
        Assert.True((await controller.ApplyAsync(resource, Value(180), gate.Current, default)).Success);
        resource.Current = Value(220);
        Assert.Equal("ForeignWriter", (await controller.ApplyAsync(resource, Value(190), gate.Current, default)).ErrorCode);
        Assert.False((await controller.RestoreAsync([resource], 2, default)).Success);
        Assert.Equal(1, resource.Writes); Assert.True(OwnedResourceController.Equivalent(Value(220), resource.Current));
    }
    [Fact] public async Task ExistingFilterAndUnfinishedJournalDeclineOwnership()
    {
        var journal = new Journal(); var gate = new Gate(); var resource = new Resource(Value(140)) { RejectOwnership = true };
        using (var controller = new OwnedResourceController(journal, Identity(), gate, () => true))
            Assert.Equal("ExistingFilter", (await controller.ApplyAsync(resource, Value(200), gate.Current, default)).ErrorCode);
        journal.Saved = Identity() with { Status = JournalStatus.Prepared };
        using var next = new OwnedResourceController(journal, Identity(), gate, () => true);
        Assert.Equal("RecoveryPending", (await next.ApplyAsync(resource, Value(200), gate.Current, default)).ErrorCode);
        Assert.Equal(0, resource.Writes);
    }
    [Fact] public async Task CrashAfterSetterCanRestorePendingValueOnlyAfterVerifiedAgentDeath()
    {
        var journal = new Journal(); var gate = new Gate(); var resource = new Resource(Value(115)) { FailAfterWrite = true };
        using (var controller = new OwnedResourceController(journal, Identity(), gate, () => true))
            Assert.False((await controller.ApplyAsync(resource, Value(180), gate.Current, default)).Success);
        Assert.Equal(JournalStatus.Prepared, journal.Saved!.Status); Assert.NotNull(journal.Saved.Resources[0].PendingValue);
        resource.FailAfterWrite = false;
        var exited = false;
        var executor = new RestoreExecutor(journal, [resource], _ => exited, () => true, "S-1-5-21-1", 1);
        Assert.False((await executor.RestoreAsync(journal.Saved, default)).Success); Assert.Equal(1, resource.Writes);
        exited = true;
        Assert.True((await executor.RestoreAsync(journal.Saved, default)).Success);
        Assert.True(OwnedResourceController.Equivalent(Value(115), resource.Current));
    }
    [Fact] public async Task IgnoredNativeSetterDoesNotReportSuccess()
    {
        var resource = new Resource(Value(100)) { IgnoreWrite = true }; var gate = new Gate();
        using var controller = new OwnedResourceController(new Journal(), Identity(), gate, () => true);
        Assert.Equal("ReadbackMismatch", (await controller.ApplyAsync(resource, Value(200), gate.Current, default)).ErrorCode);
    }
    [Fact] public async Task AtomicJournalContainsBaselineBeforeTheMemoryAdapterWrites()
    {
        using var directory = new TestDirectory(); var journal = new RecoveryJournalStore(directory.File("journal.json"));
        var gate = new Gate(); var resource = new Resource(Value(120));
        resource.BeforeWrite = () => Assert.True(File.Exists(directory.File("journal.json")));
        using var controller = new OwnedResourceController(journal, Identity(), gate, () => true);
        Assert.True((await controller.ApplyAsync(resource, Value(180), gate.Current, default)).Success);
        Assert.NotNull(await journal.LoadAsync(default));
    }
    [Fact] public void ExclusiveLeasePreventsASecondWriter()
    {
        using var directory = new TestDirectory(); var path = directory.File("writer.lease");
        using (var lease = new OwnedSessionLease(path)) Assert.Throws<IOException>(() => new OwnedSessionLease(path));
        using var afterExit = new OwnedSessionLease(path); Assert.True(afterExit.IsHeld);
    }
}
