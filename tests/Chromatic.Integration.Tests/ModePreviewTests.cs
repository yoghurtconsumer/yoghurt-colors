using System.Collections.Immutable;
using Chromatic.Agent;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Effects;
using Chromatic.Recovery;
using Xunit;

namespace Chromatic.Integration.Tests;
public sealed class ModePreviewTests
{
    private static readonly DisplayIdentity Display = new("fake-mode-monitor", null, @"\\.\DISPLAY1", 1, 2);
    private static ResourceValue Value(string text) => new([], Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text)), "ccd-v1-x64");
    private sealed class Journal : IRecoveryJournalStore
    {
        public RecoverySnapshot? Saved; public bool FailPending, FailKeep;
        public TaskCompletionSource<bool>? WaitBeforeKeep; public bool KeepEntered;
        public Task<RecoverySnapshot?> LoadAsync(CancellationToken token) => Task.FromResult(Saved);
        public async Task SaveAsync(RecoverySnapshot value, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (FailPending && value.Resources.Any(r => r.PendingValue is not null) || FailKeep && value.Preview?.Status == PreviewStatus.Confirmed)
                throw new IOException("Injected journal failure");
            if (value.Preview?.Status == PreviewStatus.Confirmed && WaitBeforeKeep is not null) { KeepEntered = true; await WaitBeforeKeep.Task; }
            Saved = value;
        }
    }
    private sealed class Mode(Journal journal) : IDisplayModeResource
    {
        public ResourceValue Actual = Value("original full topology / second monitor unchanged");
        public ResourceValue Desired = Value("new source size / second monitor unchanged");
        public int Writes; public bool Unsupported, FailSet, ForeignReadback, CrashAfterWrite;
        public TaskCompletionSource<bool>? WaitBeforeRead;
        public ResourceDescriptor Descriptor { get; } = new(OwnedResourceKind.DisplayTopology, EffectScope.GlobalDesktop);
        public ResourceValue Canonicalize(ResourceValue value) => value;
        public bool CanOwn(ResourceValue baseline) => true;
        public ResourceValue Plan(ResourceValue baseline, DisplayIdentity display, DisplayIntent intent)
        { if (Unsupported) throw new NotSupportedException("Fake unsupported mode"); return Desired; }
        public async Task<ResourceValue> ReadAsync(CancellationToken token)
        { if (WaitBeforeRead is not null) await WaitBeforeRead.Task; return Actual; }
        public Task WriteAsync(ResourceValue value, CancellationToken token)
        {
            Assert.NotNull(journal.Saved);
            Assert.True(journal.Saved!.Status is JournalStatus.Prepared or JournalStatus.Restoring);
            Writes++;
            if (FailSet && value == Desired) throw new IOException("Fake setter failed before mutation");
            Actual = value == Desired && ForeignReadback ? Value("foreign topology") : value;
            if (CrashAfterWrite && value == Desired) throw new IOException("Fake agent died immediately after write");
            return Task.CompletedTask;
        }
    }
    private sealed class Colors : IColorBackend
    {
        public int Restores; public TaskCompletionSource<bool>? WaitBeforeRestore;
        public Task<ApplyResult> ApplyAsync(ColorSettings color, long revision, CancellationToken token) => throw new Exception("Preview must never apply colours");
        public async Task<ApplyResult> RestoreAsync(CancellationToken token) { Restores++; if (WaitBeforeRestore is not null) await WaitBeforeRestore.Task; return new(true, "FakeColors", null, "Restored fake colors", 0); }
    }
    private sealed class Clock : TimeProvider
    {
        private long _ticks; private readonly List<Timer> _timers = [];
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _ticks);
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan due, TimeSpan period)
        {
            var timer = new Timer(this, callback, state); timer.Change(due, period); lock (_timers) _timers.Add(timer); return timer;
        }
        public void Advance(TimeSpan time)
        {
            Interlocked.Add(ref _ticks, time.Ticks); Timer[] timers; lock (_timers) timers = _timers.ToArray();
            foreach (var timer in timers) if (timer.Due <= GetTimestamp() && !timer.Stopped) { timer.Stopped = true; timer.Fire(); }
        }
        private sealed class Timer(Clock clock, TimerCallback callback, object? state) : ITimer
        {
            public long Due; public bool Stopped;
            public bool Change(TimeSpan due, TimeSpan period) { Due = due == Timeout.InfiniteTimeSpan ? long.MaxValue : clock.GetTimestamp() + due.Ticks; return true; }
            public void Fire() => callback(state);
            public void Dispose() => Stopped = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
    private sealed class Fixture : IDisposable
    {
        public AgentStaWorker Worker = new(); public Clock Clock = new(); public Journal Journal = new(); public Mode Mode;
        public JournaledEffectsCoordinator Coordinator; public OwnedResourceController Owned; public Colors Colors = new();
        public Fixture()
        {
            Mode = new(Journal); Coordinator = new(Worker, clock: Clock) { RecoveryReady = true }; Coordinator.AttachBackend(Colors);
            Owned = new(Journal, Identity(), Coordinator.ModeSafetyGate, () => true); Coordinator.AttachDisplayPreview(Mode, Owned);
        }
        public Task<EffectsAcknowledgment> Send(EffectsCommand command) => Coordinator.SendAsync(new(Guid.NewGuid(), Coordinator.State.Revision, Coordinator.State.Generation, command));
        public Task<EffectsAcknowledgment> Begin() => Send(new PreviewMode(Guid.NewGuid(), Display, new(1280, 960, new(144, 1)), DateTimeOffset.MaxValue));
        public void Dispose() { Worker.Dispose(); Owned.Dispose(); }
    }
    private static RecoverySnapshot Identity() => new() { UserSid = "S-1-5-21-1", SessionId = 1, AgentPid = 123, AgentStartTicks = 456 };
    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        while (!condition()) await Task.Delay(5, timeout.Token);
    }
    [Fact] public async Task PreviewJournalsBeforeWriteUsesAgentDeadlineAndRestoresExactBaseline()
    {
        using var f = new Fixture(); var baseline = f.Mode.Actual; var ack = await f.Begin();
        Assert.True(ack.Completion.Success, ack.Completion.Explanation); Assert.Equal(PreviewStatus.AwaitingConfirmation, f.Coordinator.State.Preview!.Status);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(15), f.Coordinator.State.Preview.DeadlineUtc);
        Assert.False(f.Coordinator.State.Enabled); Assert.Equal(1, f.Colors.Restores); Assert.Equal(f.Mode.Desired, f.Mode.Actual);
        var cancel = await f.Send(new CancelPreview(f.Coordinator.State.Preview.Id));
        Assert.True(cancel.Completion.Success); Assert.Equal(baseline, f.Mode.Actual); Assert.Equal(PreviewStatus.Reverted, f.Coordinator.State.Preview.Status);
        Assert.Equal(JournalStatus.Restored, f.Journal.Saved!.Status); Assert.Empty(f.Journal.Saved.Resources);
    }
    [Fact] public async Task MonotonicDeadlineRevertsWithoutAnyUiCommands()
    {
        using var f = new Fixture(); var baseline = f.Mode.Actual; Assert.True((await f.Begin()).Completion.Success);
        f.Clock.Advance(TimeSpan.FromSeconds(15)); await Until(() => f.Coordinator.State.Preview?.Status == PreviewStatus.Reverted);
        Assert.Equal(baseline, f.Mode.Actual); Assert.Equal(2, f.Mode.Writes);
    }
    [Fact] public async Task DeadlineFencesAQueuedWriteBeforeItCanMutate()
    {
        using var f = new Fixture(); var baseline = f.Mode.Actual;
        f.Mode.WaitBeforeRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var applying = f.Begin(); await Until(() => f.Journal.Saved?.Preview is not null);
        f.Clock.Advance(TimeSpan.FromSeconds(15));
        await Until(() => f.Coordinator.State.Preview?.Status == PreviewStatus.RollingBack);
        f.Mode.WaitBeforeRead.SetResult(true); Assert.False((await applying).Completion.Success);
        await Until(() => !f.Coordinator.State.PreviewActive);
        Assert.Equal(0, f.Mode.Writes); Assert.Equal(baseline, f.Mode.Actual);
    }
    [Fact] public async Task MismatchingReadbackIsNotClaimedAndForeignValuesAreNotOverwritten()
    {
        using var f = new Fixture(); f.Mode.ForeignReadback = true;
        Assert.False((await f.Begin()).Completion.Success); Assert.True(f.Coordinator.State.RecoveryPending);
        Assert.Equal(Value("foreign topology"), f.Mode.Actual); Assert.Equal(1, f.Mode.Writes);
    }
    [Fact] public async Task KeepBeforeDeadlineReleasesOwnershipButDoesNotWriteAgainOrRestoreOnExit()
    {
        using var f = new Fixture(); await f.Begin(); var id = f.Coordinator.State.Preview!.Id;
        Assert.True((await f.Send(new ConfirmMode(id))).Completion.Success);
        f.Clock.Advance(TimeSpan.FromMinutes(1)); await f.Send(new Shutdown());
        Assert.Equal(f.Mode.Desired, f.Mode.Actual); Assert.Equal(1, f.Mode.Writes); Assert.Equal(PreviewStatus.Confirmed, f.Coordinator.State.Preview.Status);
        Assert.Equal(JournalStatus.Restored, f.Journal.Saved!.Status); Assert.Equal(PreviewStatus.Confirmed, f.Journal.Saved.Preview!.Status);
    }
    [Theory] [InlineData("pending")] [InlineData("set")] [InlineData("unsupported")] [InlineData("after-write")]
    public async Task FailureAtEachWriteStageCompensatesWithoutFakeSuccess(string step)
    {
        using var f = new Fixture(); var baseline = f.Mode.Actual;
        f.Journal.FailPending = step == "pending"; f.Mode.FailSet = step == "set"; f.Mode.Unsupported = step == "unsupported"; f.Mode.CrashAfterWrite = step == "after-write";
        var ack = await f.Begin(); Assert.False(ack.Completion.Success); Assert.Equal(baseline, f.Mode.Actual);
        Assert.False(f.Coordinator.State.PreviewActive); if (step is "pending" or "unsupported") Assert.Equal(0, f.Mode.Writes);
    }
    [Fact] public async Task ForeignWriterIsPreservedAndRecoveryIsRequired()
    {
        using var f = new Fixture(); await f.Begin(); f.Mode.Actual = Value("foreign topology");
        var ack = await f.Send(new CancelPreview(f.Coordinator.State.Preview!.Id));
        Assert.False(ack.Completion.Success); Assert.Equal(Value("foreign topology"), f.Mode.Actual); Assert.True(f.Coordinator.State.RecoveryPending);
        Assert.Equal(JournalStatus.Conflict, f.Journal.Saved!.Status);
    }
    [Fact] public async Task FailedKeepSaveRevertsAndNewPreviewCanCaptureANewBaseline()
    {
        using var f = new Fixture(); var baseline = f.Mode.Actual; await f.Begin(); f.Journal.FailKeep = true;
        Assert.False((await f.Send(new ConfirmMode(f.Coordinator.State.Preview!.Id))).Completion.Success); Assert.Equal(baseline, f.Mode.Actual);
        f.Journal.FailKeep = false; f.Mode.Actual = Value("new externally selected baseline"); var second = f.Mode.Actual;
        await f.Begin(); await f.Send(new CancelPreview(f.Coordinator.State.Preview!.Id)); Assert.Equal(second, f.Mode.Actual);
    }
    [Fact] public async Task SuspendAndShutdownHavePriorityAndProfilesCannotInterruptPreview()
    {
        using var f = new Fixture(); var baseline = f.Mode.Actual; await f.Begin();
        Assert.Equal(CompletionStatus.Blocked, (await f.Send(new SetColor(new(180)))).Decision.Status);
        var generation = f.Coordinator.State.Generation; await f.Send(new SetHold(false)); Assert.Equal(generation, f.Coordinator.State.Generation);
        Assert.Equal(PreviewStatus.AwaitingConfirmation, f.Coordinator.State.Preview!.Status); Assert.Equal(f.Mode.Desired, f.Mode.Actual);
        Assert.True((await f.Send(new Suspend(SuspensionReason.SessionLock))).Completion.Success); Assert.Equal(baseline, f.Mode.Actual);
    }
    [Fact] public async Task NewPreviewIsBlockedUntilKeepIsDurableAndCannotStealItsTimer()
    {
        using var f = new Fixture(); await f.Begin();
        f.Journal.WaitBeforeKeep = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var keeping = f.Send(new ConfirmMode(f.Coordinator.State.Preview!.Id)); await Until(() => f.Journal.KeepEntered);
        var blocked = await f.Begin(); Assert.Equal(CompletionStatus.Blocked, blocked.Decision.Status);
        f.Journal.WaitBeforeKeep.SetResult(true); Assert.True((await keeping).Completion.Success);
        var kept = f.Mode.Actual; await f.Begin(); f.Clock.Advance(TimeSpan.FromSeconds(15));
        await Until(() => f.Coordinator.State.Preview?.Status == PreviewStatus.Reverted); Assert.Equal(kept, f.Mode.Actual);
    }
    [Fact] public async Task CancelDuringColourRestoreAfterKeepCannotLeavePreviewLatched()
    {
        using var f = new Fixture(); await f.Begin(); await f.Send(new ConfirmMode(f.Coordinator.State.Preview!.Id));
        var kept = f.Mode.Actual; f.Colors.WaitBeforeRestore = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var beginning = f.Begin(); await Until(() => f.Colors.Restores == 2);
        var id = f.Coordinator.State.Preview!.Id; Assert.True((await f.Send(new CancelPreview(id))).Completion.Success);
        Assert.Equal(PreviewStatus.Reverted, f.Coordinator.State.Preview.Status);
        Assert.False((await f.Begin()).Completion.Success); // Previous begin is still draining.
        f.Colors.WaitBeforeRestore.SetResult(true); Assert.False((await beginning).Completion.Success);
        Assert.Equal(kept, f.Mode.Actual); Assert.Equal(1, f.Mode.Writes); Assert.False(f.Coordinator.State.PreviewActive);
    }
    [Fact] public async Task StaleCancelStillRestoresButOldTransactionCannotCancelNewPreview()
    {
        using var f = new Fixture(); var baseline = f.Mode.Actual; await f.Begin(); var first = f.Coordinator.State.Preview!.Id;
        var cancelled = await f.Coordinator.SendAsync(new(Guid.NewGuid(), 0, 0, new CancelPreview(first)));
        Assert.True(cancelled.Completion.Success); Assert.Equal(baseline, f.Mode.Actual);
        await f.Begin(); var second = f.Coordinator.State.Preview!.Id;
        Assert.NotEqual(first, second); Assert.False((await f.Send(new CancelPreview(first))).Completion.Success);
        Assert.Equal(PreviewStatus.AwaitingConfirmation, f.Coordinator.State.Preview.Status);
        await f.Send(new CancelPreview(second)); Assert.Equal(baseline, f.Mode.Actual);
    }
    [Fact] public async Task RecoveryRestoresPendingTopologyOnlyAfterVerifiedAgentDeathAndLease()
    {
        var journal = new Journal(); var mode = new Mode(journal); var baseline = mode.Actual;
        var snapshot = Identity() with { Status = JournalStatus.Prepared, Preview = new(Guid.NewGuid(), PreviewStatus.Applying, Display, new(1280, 960, new(144, 1)), DateTimeOffset.UtcNow.AddSeconds(15), 1),
            Resources = [new(Guid.NewGuid(), OwnedResourceKind.DisplayTopology, EffectScope.GlobalDesktop, null, baseline, baseline, 1, PendingValue: mode.Desired)] };
        journal.Saved = snapshot; mode.Actual = mode.Desired;
        var denied = await new RestoreExecutor(journal, [mode], _ => false, () => true, snapshot.UserSid, snapshot.SessionId).RestoreAsync(snapshot, default);
        Assert.False(denied.Success); Assert.Equal(0, mode.Writes);
        var restored = await new RestoreExecutor(journal, [mode], _ => true, () => true, snapshot.UserSid, snapshot.SessionId).RestoreAsync(snapshot, default);
        Assert.True(restored.Success); Assert.Equal(baseline, mode.Actual);
    }
}
