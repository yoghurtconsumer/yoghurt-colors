using Chromatic.Core.Models;
using Chromatic.Core.State;
using Xunit;

namespace Chromatic.Core.Tests;
public sealed class ReducerTests
{
    [Fact] public void UnknownSuspensionAndInvalidForegroundAreRejected()
    {
        var state = new EffectsState();
        Assert.Equal(CompletionStatus.Invalid, Dispatch(state, new Suspend((SuspensionReason)999)).Result.Status);
        Assert.Equal(CompletionStatus.Invalid, Dispatch(state, new ForegroundChanged(new("game.exe", 0, 0), null)).Result.Status);
    }
    private static Reduction Dispatch(EffectsState state, EffectsCommand command) =>
        EffectsReducer.Reduce(state, new(Guid.NewGuid(), state.Revision, state.Generation, command));
    private static EffectsState Enabled() => new() { Enabled = true, HoldBoost = new(true) };

    [Fact] public void DefaultIsSafeAndEveryServiceIsOff()
    {
        var defaults = new AppSettings();
        Assert.False(defaults.EffectsEnabled); Assert.False(defaults.Crosshair.Enabled); Assert.False(defaults.HoldBoost.Enabled);
        Assert.Equal(ColorSettings.Neutral, defaults.GlobalColor);
        Assert.Equal(new OptionalServices(), defaults.Services);
        Assert.Equal(CloseBehavior.ExitAndRestore, defaults.CloseBehavior);
        Assert.False(new EffectsState().Effective.Enabled);
    }
    [Fact] public void HoldIsTemporaryAndReleaseUsesNewestPreset()
    {
        var original = Enabled();
        var held = Dispatch(original, new SetHold(true)).State;
        var newProfile = new DisplayProfile { Id = "night", GlobalColor = new(180) };
        var changed = Dispatch(held, new ActivatePreset(newProfile)).State;
        Assert.NotNull(changed.Effective.HoldModifier);
        Assert.Equal(newProfile.GlobalColor, changed.Effective.GlobalColor);
        var released = Dispatch(changed, new SetHold(false)).State;
        Assert.Null(released.Effective.HoldModifier); Assert.Equal(180, released.Effective.GlobalColor.SaturationPercent);
        Assert.False(original.Held); Assert.Equal(100, original.DesktopProfile.GlobalColor.SaturationPercent);
    }
    [Fact] public void StaleColorCannotOverwriteNewerState()
    {
        var original = Enabled(); var next = Dispatch(original, new SetColor(new(120))).State;
        var stale = EffectsReducer.Reduce(next, new(Guid.NewGuid(), 0, 0, new SetColor(new(300))));
        Assert.Equal(CompletionStatus.StaleRevision, stale.Result.Status); Assert.Same(next, stale.State);
        Assert.False(EffectsReducer.CanWrite(next, original.Revision, original.Generation));
    }
    [Theory] [InlineData("restore")] [InlineData("shutdown")] [InlineData("suspend")] [InlineData("release")] [InlineData("disable")]
    public void SafetyCommandsBypassStaleRevisionAndFenceQueuedWrites(string operation)
    {
        var state = Dispatch(Enabled(), new SetHold(true)).State;
        EffectsCommand command = operation switch { "restore" => new Restore(), "shutdown" => new Shutdown(),
            "suspend" => new Suspend(SuspensionReason.SessionLock), "disable" => new Enable(false), _ => new SetHold(false) };
        var result = EffectsReducer.Reduce(state, new(Guid.NewGuid(), 0, 0, command));
        Assert.Equal(CompletionStatus.Accepted, result.Result.Status); Assert.False(result.State.Held);
        Assert.True(result.State.Generation > state.Generation);
        Assert.False(EffectsReducer.CanWrite(result.State, state.Revision, state.Generation));
    }
    [Fact] public void SuspensionReasonsStackAndResumeDoesNotReactivateHold()
    {
        var state = Dispatch(Enabled(), new SetHold(true)).State;
        state = Dispatch(state, new Suspend(SuspensionReason.SessionLock)).State;
        state = Dispatch(state, new Suspend(SuspensionReason.PowerSuspend)).State;
        state = Dispatch(state, new Resume(SuspensionReason.SessionLock)).State;
        Assert.False(state.Effective.Enabled);
        state = Dispatch(state, new Resume(SuspensionReason.PowerSuspend)).State;
        Assert.False(state.Effective.Enabled); Assert.False(state.Held);
        Assert.True(Dispatch(state, new ApplyProfile(new())).State.Effective.Enabled);
    }
    [Fact] public void ExplicitApplyIsAtomicAndResumesOnlyUserPause()
    {
        var state = Dispatch(Enabled(), new Suspend(SuspensionReason.Pause)).State;
        var result = Dispatch(state, new ApplyProfile(new() { GlobalColor = new(175) }));
        Assert.Equal(CompletionStatus.Accepted, result.Result.Status); Assert.Equal(state.Revision + 1, result.State.Revision);
        Assert.True(result.State.Effective.Enabled); Assert.False(result.State.Held);
        Assert.Equal(175, result.State.BaseProfile.GlobalColor.SaturationPercent);
    }
    [Theory] [InlineData(SuspensionReason.SessionLock)] [InlineData(SuspensionReason.PowerSuspend)]
    [InlineData(SuspensionReason.SessionDisconnected)] [InlineData(SuspensionReason.AdvancedColor)] [InlineData(SuspensionReason.Recovery)]
    public void ExplicitApplyCannotRemoveSafetySuspensions(SuspensionReason reason)
    {
        var state = Dispatch(Enabled(), new Suspend(reason)).State;
        var result = Dispatch(state, new ApplyProfile(new()));
        Assert.Equal(CompletionStatus.Blocked, result.Result.Status); Assert.Same(state, result.State); Assert.False(result.State.Effective.Enabled);
        Assert.Equal(CompletionStatus.Blocked, Dispatch(state, new Enable(true)).Result.Status);
        var resumed = Dispatch(state, new Resume(reason)).State;
        Assert.False(resumed.Enabled); Assert.False(resumed.Effective.Enabled);
    }
    [Fact] public void RecoveryAndShutdownBlockActivation()
    {
        var recovery = Enabled() with { RecoveryPending = true };
        Assert.Equal(CompletionStatus.Blocked, Dispatch(recovery, new Enable(true)).Result.Status);
        var restored = Dispatch(recovery, new Restore()).State;
        Assert.True(restored.RecoveryPending); // requesting restore is not proof of restored hardware
        var shutdown = Dispatch(Enabled(), new Shutdown()).State;
        Assert.Equal(CompletionStatus.Blocked, Dispatch(shutdown, new Enable(true)).Result.Status);
    }
    [Fact] public void PreviewClearsHoldAndCannotConfirmBeforeNativeApply()
    {
        var id = Guid.NewGuid();
        var state = Dispatch(Dispatch(Enabled(), new SetHold(true)).State,
            new PreviewMode(id, new("monitor-a", "serial", "DISPLAY1", 1, 1), new(1920, 1080, new(60000, 1001)), DateTimeOffset.UnixEpoch)).State;
        Assert.True(state.PreviewActive); Assert.False(state.Held); Assert.False(state.Effective.Enabled);
        Assert.Equal(CompletionStatus.Blocked, Dispatch(state, new ConfirmMode(id)).Result.Status);
        Assert.Equal(CompletionStatus.Blocked, Dispatch(state, new CancelPreview(Guid.NewGuid())).Result.Status);
        var cancelled = Dispatch(state, new CancelPreview(id)).State;
        Assert.Equal(PreviewStatus.RollingBack, cancelled.Preview!.Status);
        Assert.False(cancelled.Effective.Enabled); // wait for real rollback acknowledgment
    }
    [Fact] public void ForegroundClearsHoldAndManualOverride()
    {
        var state = Dispatch(Dispatch(Enabled(), new SetHold(true)).State, new SetColor(new(200))).State;
        var game = new DisplayProfile { Id = "game", GlobalColor = new(125) };
        state = Dispatch(state, new ForegroundChanged(new("C:\\Games\\Game.exe", 12, 100), game)).State;
        Assert.False(state.Held); Assert.Equal("game", state.BaseProfile.Id);
        state = Dispatch(state, new ForegroundChanged(null, null)).State;
        Assert.Equal("desktop", state.BaseProfile.Id);
    }
    [Fact] public void ObservedStateRequiresMatchingRevisionAndGeneration()
    {
        var state = Dispatch(Enabled(), new SetColor(new(120))).State;
        var result = new ApplyResult(false, "Fake", "Rejected", "Driver rejected", state.Revision);
        Assert.Same(state, EffectsReducer.Observe(state, result, 0));
        var observed = EffectsReducer.Observe(state, result, state.Generation);
        Assert.False(observed.Observed.LastResult!.Success); Assert.True(observed.Enabled);
    }
    [Fact] public void InvalidColorDoesNotChangeState()
    {
        var state = Enabled(); var result = Dispatch(state, new SetColor(new(double.NaN)));
        Assert.Same(state, result.State); Assert.Equal(CompletionStatus.Invalid, result.Result.Status);
    }
}
