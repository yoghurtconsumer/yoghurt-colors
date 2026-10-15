using Chromatic.Agent;
using Chromatic.Agent.Backends;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Effects;
using Chromatic.Native;
using Chromatic.Infrastructure.Ipc;
using Xunit;

namespace Chromatic.Integration.Tests;

public sealed class StaCoordinatorTests
{
    [Fact] public async Task SlowGammaRouteIsRolledBackAndDisabledForTheSession()
    {
        using var worker = new AgentStaWorker(); var coordinator = new JournaledEffectsCoordinator(worker) { RecoveryReady = true };
        var journal = new DelayedJournal(); journal.Release.SetResult(); var matrix = new MemoryResource();
        var display = new DisplayIdentity("fake-monitor", null, @"\\.\DISPLAY1", 1, 1); var api = new SlowGamma();
        using var gamma = new GammaResource(display, () => api, _ => true, () => true);
        using var owned = new OwnedResourceController(journal, new() { UserSid = "S-1-5-21-1", SessionId = 1, AgentPid = 42, AgentStartTicks = 123 }, coordinator, () => true);
        coordinator.AttachBackend(new PlannedColorBackend(owned, [matrix, gamma], matrix, () => coordinator.State, new(GammaCompatibility: true)));
        Task<EffectsAcknowledgment> Send(EffectsCommand command) => coordinator.SendAsync(new(Guid.NewGuid(), coordinator.State.Revision, coordinator.State.Generation, command));
        await Send(new ActivatePreset(new() { GlobalColor = new(Gamma: 1.2), Monitors = [new(display)] }));
        var result = await Send(new Enable(true));
        Assert.False(result.Completion.Success); Assert.Equal("GammaSlow", result.Completion.ErrorCode);
        Assert.Equal(JournalStatus.Restored, owned.Snapshot.Status);
        Assert.Equal(Enumerable.Range(0, 768).Select(i => (ushort)((i % 256) * 257)).ToArray(), api.Value.Values.ToArray());
        var next = await Send(new SetColor(new(Gamma: 1.3)));
        Assert.Equal("GammaUnavailable", next.Completion.ErrorCode);
        Assert.Equal(2, api.Writes); // one slow apply and one exact baseline restore
    }
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task NewGenerationFencesPendingWritesAndStaleFailureCannotRollbackNewerApply(bool disable, bool throughPipe)
    {
        using var worker = new AgentStaWorker();
        var coordinator = new JournaledEffectsCoordinator(worker) { RecoveryReady = true };
        var journal = new DelayedJournal(); var resource = new MemoryResource();
        using var owned = new OwnedResourceController(journal, new() { UserSid = "S-1-5-21-1", SessionId = 1, AgentPid = 42, AgentStartTicks = 123 }, coordinator, () => true);
        coordinator.AttachBackend(new PlannedColorBackend(owned, [resource], resource, () => coordinator.State, new()));
        var own = PipeInterop.Current;
        var launch = new PipeLaunchIdentity(PipeAuthorization.CreatePipeName(own.SessionId), PipeAuthorization.CreateNonce(), CompanionLaunch.Peer(own));
        using var server = PipeInterop.CreateCurrentUserLocalServer(launch.PipeName);
        using var lifetime = new CancellationTokenSource();
        var accepting = AuthenticatedPipe.AcceptAsync(server, launch, lifetime.Token);
        using var pipe = await AuthenticatedPipe.ConnectAsync(launch, lifetime.Token); await accepting;
        using var remote = new EffectsPipeClient(pipe);
        var serving = AuthenticatedPipe.ServeCommandsAsync(server, own.SessionId, coordinator, lifetime.Token);
        IEffectsClient client = throughPipe ? remote : coordinator;
        Task<EffectsAcknowledgment> Send(EffectsCommand command) => client.SendAsync(
            new(Guid.NewGuid(), client.State.Revision, client.State.Generation, command));
        await Send(new SetColor(new(SaturationPercent: 180)));
        var old = Send(new Enable(true));
        await journal.Prepared.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (throughPipe) await remote.QueryAsync();
        var newer = Send(disable ? new Enable(false) : new SetColor(new(SaturationPercent: 220)));
        using var accepted = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (coordinator.State.Generation != 3) await Task.Delay(10, accepted.Token);
        Assert.Equal(3, coordinator.State.Generation);
        journal.Release.TrySetResult();
        var first = await old.WaitAsync(TimeSpan.FromSeconds(5));
        var latest = await newer.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(first.Completion.Success); Assert.True(latest.Completion.Success, latest.Completion.Explanation);
        Assert.Equal(disable ? 0 : 1, resource.Writes);
        Assert.Equal(ColorMatrixBuilder.Saturation(disable ? 100 : 220).Values.ToArray(), resource.Current.Values.ToArray());
        Assert.Equal(3, coordinator.State.Observed.Generation);
        lifetime.Cancel();
        try { await serving.WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (Exception error) when (error is OperationCanceledException or IOException) { }
    }
    [Fact] public async Task StateQueryDoesNotAdvanceRevisionOrCallBackend()
    {
        using var worker = new AgentStaWorker(); var coordinator = new JournaledEffectsCoordinator(worker);
        var result = await coordinator.SendAsync(new(Guid.NewGuid(), 987, 654, new GetState()));
        Assert.True(result.Completion.Success); Assert.Equal(0, result.State.Revision); Assert.Equal(0, result.State.Generation);
    }
    private sealed class DelayedJournal : IRecoveryJournalStore
    {
        public TaskCompletionSource Prepared = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private RecoverySnapshot? _value; private bool _delayed;
        public Task<RecoverySnapshot?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(_value);
        public async Task SaveAsync(RecoverySnapshot snapshot, CancellationToken cancellationToken)
        {
            if (!_delayed && snapshot.Status == JournalStatus.Prepared)
            { _delayed = true; Prepared.TrySetResult(); await Release.Task; }
            _value = snapshot;
        }
    }
    private sealed class MemoryResource : IResourceAdapter
    {
        public ResourceValue Current = new(Matrix5x5.Identity.Values); public int Writes;
        public ResourceDescriptor Descriptor { get; } = new(OwnedResourceKind.GlobalMatrix, EffectScope.GlobalDesktop);
        public ResourceValue Canonicalize(ResourceValue value) => value;
        public bool CanOwn(ResourceValue value) => true;
        public Task<ResourceValue> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Current);
        public Task WriteAsync(ResourceValue value, CancellationToken cancellationToken) { Current = value; Writes++; return Task.CompletedTask; }
    }
    private sealed class SlowGamma : IGammaApi
    {
        public GammaRamp Value = new(Enumerable.Range(0, 768).Select(i => (ushort)((i % 256) * 257)));
        public int Writes;
        public GammaRamp Read() => Value;
        public void Write(GammaRamp ramp) { Thread.Sleep(275); Value = ramp; Writes++; }
        public void Dispose() { }
    }
}
