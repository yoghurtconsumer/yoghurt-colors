using System.Collections.Immutable;
using Chromatic.Agent;
using Chromatic.Agent.Backends;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Core.Validation;
using Chromatic.Infrastructure.Persistence;
using Chromatic.Recovery;
using Xunit;
namespace Chromatic.Integration.Tests;
public sealed class RecoveryTests
{
    public static RecoverySnapshot ValidSnapshot() => new()
    {
        UserSid = "S-1-5-21-1", SessionId = 1, AgentPid = 123, AgentStartTicks = 100, Generation = 2,
        Resources = [new(Guid.NewGuid(), OwnedResourceKind.GlobalMatrix, EffectScope.GlobalDesktop, null,
            new(Enumerable.Range(0, 25).Select(i => i % 6 == 0 ? 1d : 0d).ToImmutableArray()),
            new(Enumerable.Range(0, 25).Select(i => i % 6 == 0 ? 1d : 0d).ToImmutableArray()), 2)]
    };
    [Fact] public async Task JournalRoundTripsExactOwnedBaselineAndDoesNotPretendToRestore()
    {
        using var folder = new TestDirectory(); var journal = new RecoveryJournal(folder.File("journal.json"));
        var snapshot = ValidSnapshot(); await journal.WriteBeforeMutationAsync(snapshot);
        var read = (await journal.ReadAsync()).Value;
        Assert.Equal(snapshot.JournalId, read.JournalId); Assert.Equal(snapshot.Resources[0].Baseline.Values.ToArray(), read.Resources[0].Baseline.Values.ToArray());
        Assert.False((await new RestoreExecutor().RestoreAsync(read, default)).Success);
    }
    [Fact] public void JournalRejectsWrongScopeAndFabricatedBaselineShape()
    {
        var snapshot = ValidSnapshot(); var resource = snapshot.Resources[0];
        Assert.Throws<ArgumentException>(() => RecoveryValidator.Validate(snapshot with { Resources = [resource with { Scope = EffectScope.PerMonitor }] }));
        Assert.Throws<ArgumentException>(() => RecoveryValidator.Validate(snapshot with { Resources = [resource with { Baseline = new([1, 2]) }] }));
    }
    [Fact] public async Task CoordinatorRestoresCapturedFakeBaselineAndRejectsOldWrites()
    {
        var fake = new FakeColorBackend(); using var coordinator = new EffectsCoordinator(fake);
        var enabled = await coordinator.SendAsync(new(Guid.NewGuid(), 0, 0, new Enable(true)));
        Assert.True(enabled.Completion.Success); Assert.Equal(100, fake.Current.SaturationPercent);
        var revision = coordinator.State.Revision; var generation = coordinator.State.Generation;
        var restored = await coordinator.SendAsync(new(Guid.NewGuid(), 0, 0, new Restore()));
        Assert.True(restored.Completion.Success); Assert.Equal(110, fake.Current.SaturationPercent);
        var old = await coordinator.SendAsync(new(Guid.NewGuid(), revision, generation, new SetColor(new(300))));
        Assert.Equal(CompletionStatus.StaleRevision, old.Decision.Status); Assert.Single(fake.Applied);
    }
    [Fact] public async Task BackendFailureRemainsVisibleInObservedState()
    {
        using var coordinator = new EffectsCoordinator(new FakeColorBackend { FailApply = true });
        var ack = await coordinator.SendAsync(new(Guid.NewGuid(), 0, 0, new Enable(true)));
        Assert.Equal(CompletionStatus.Accepted, ack.Decision.Status); Assert.False(ack.Completion.Success);
        Assert.Equal("InjectedFailure", coordinator.State.Observed.LastResult!.ErrorCode);
    }
    [Fact] public async Task NativeBackendsNeverReturnFakeSuccess()
    {
        var display = new DisplayIdentity("monitor", null, "DISPLAY1", 1, 1);
        var results = new[]
        {
            await new MagnificationBackend().ApplyAsync(new(), 1, default), await new MagnificationBackend().RestoreAsync(default),
            await new NvidiaDvcBackend().ApplyAsync(display, .5, 1, default), await new NvidiaDvcBackend().RestoreAsync(default),
            await new SdrGammaBackend().ApplyAsync(new(display), null, 1, default), await new SdrGammaBackend().RestoreAsync(default),
            await new DisplayModeBackend().PreviewAsync(new(Guid.NewGuid(), PreviewStatus.Prepared, display, new(1920, 1080, new(60, 1)), DateTimeOffset.UtcNow, 1), 1, default),
            await new DisplayModeBackend().RestoreAsync(default), await new HdrBackend().SetAsync(display, true, 1, default),
            await new DdcMonitorBackend().SetAsync(display, Chromatic.Core.Abstractions.MonitorControl.Brightness, 50, 1, default),
            await new DdcMonitorBackend().UndoAsync(default)
        };
        Assert.All(results, result => { Assert.False(result.Success); Assert.Equal("NotImplemented", result.ErrorCode); });
    }
}
