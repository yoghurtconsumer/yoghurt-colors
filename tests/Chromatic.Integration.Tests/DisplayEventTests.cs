using Chromatic.Agent;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Native;
using Xunit;

namespace Chromatic.Integration.Tests;

public sealed class DisplayEventTests
{
    private static readonly DisplayIdentity Display = new("fake-monitor", "serial", @"\\.\DISPLAY1", 1, 1);
    private static DisplaySafetyReport Sdr => new([Display], true, false, "Injected SDR");
    private static CapabilityReport Caps(DisplaySafetyReport report) => new([new(BackendKind.Magnification,
        report.AllSdr ? Capability.Available : Capability.Unsupported, report.Reason, EffectScope.GlobalDesktop)], !report.AllSdr);
    private static DisplayProfile Profile(bool native) => new()
    { GlobalColor = native ? new(Vibrance: .5) : new(180), Monitors = native ? [new(Display)] : [] };

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ResolutionNotificationsPreserveConfirmedRouteWithoutRestoreOrReapply(bool native)
    {
        using var worker = new AgentStaWorker(); var backend = new CountingBackend(native);
        var coordinator = new JournaledEffectsCoordinator(worker, new() { Capabilities = Caps(Sdr) }) { RecoveryReady = true };
        coordinator.AttachBackend(backend);
        var applied = await coordinator.SendAsync(new(Guid.NewGuid(), 0, 0, new ApplyProfile(Profile(native))));
        Assert.True(applied.Completion.Success);
        var events = new AgentDisplayEvents(coordinator, Sdr, () => Sdr, Caps, _ => { });
        for (var i = 0; i < 20; i++) await events.HandleAsync(AgentBroadcast.DisplayChanged);
        Assert.True(coordinator.State.Effective.Enabled);
        Assert.Equal(applied.State.Revision, coordinator.State.Revision);
        Assert.Equal(applied.State.Generation, coordinator.State.Generation);
        Assert.Equal(1, backend.Applies); Assert.Equal(0, backend.Restores);
    }
    [Theory] [InlineData("hdr")] [InlineData("unknown")] [InlineData("remap")]
    [InlineData("topology")] [InlineData("lock")] [InlineData("sleep")]
    public async Task RealSafetyChangesStillRestoreAndNeverAutomaticallyEnable(string scenario)
    {
        using var worker = new AgentStaWorker(); var backend = new CountingBackend(false);
        var coordinator = new JournaledEffectsCoordinator(worker, new() { Capabilities = Caps(Sdr) }) { RecoveryReady = true };
        coordinator.AttachBackend(backend);
        await coordinator.SendAsync(new(Guid.NewGuid(), 0, 0, new ApplyProfile(Profile(false))));
        var next = scenario switch
        {
            "hdr" => Sdr with { AdvancedColorActive = true },
            "unknown" => Sdr with { Known = false },
            "remap" => Sdr with { Displays = [Display with { TargetId = 2 }] },
            "topology" => Sdr with { Displays = [Display, Display with { MonitorDevicePath = "other" }] },
            _ => Sdr
        };
        var events = new AgentDisplayEvents(coordinator, Sdr, () => next, Caps, _ => { });
        await events.HandleAsync(scenario == "lock" ? AgentBroadcast.SessionSuspended :
            scenario == "sleep" ? AgentBroadcast.PowerSuspended : AgentBroadcast.DisplayChanged);
        Assert.False(coordinator.State.Effective.Enabled); Assert.True(backend.Restores > 0);
        if (scenario is "lock" or "sleep")
            await events.HandleAsync(scenario == "lock" ? AgentBroadcast.SessionUnlocked : AgentBroadcast.PowerResumed);
        Assert.False(coordinator.State.Enabled); Assert.Equal(1, backend.Applies);
    }
    [Theory] [InlineData("pending")] [InlineData("failed")] [InlineData("generation")]
    [InlineData("gamma")] [InlineData("warmth")] [InlineData("held")]
    [InlineData("pause")] [InlineData("route")] [InlineData("nativeSaturation")]
    public void UnconfirmedOrTonalRoutesCannotTakeThePreserveShortcut(string scenario)
    {
        var native = scenario == "nativeSaturation";
        var state = new EffectsState { Enabled = true, Revision = 5, Generation = 6,
            ManualProfile = Profile(native), Observed = new(5, 6, new(true, native ? "Magnification+NvidiaDvc" : "Magnification", null, null, 5)) };
        state = scenario switch
        {
            "pending" => state with { Revision = 7 },
            "failed" => state with { Observed = state.Observed with { LastResult = state.Observed.LastResult! with { Success = false } } },
            "generation" => state with { Generation = 8 },
            "gamma" => state with { ManualProfile = Profile(false) with { GlobalColor = new(180, Gamma: 1.2) } },
            "warmth" => state with { ManualProfile = Profile(false) with { Monitors = [new(Display, Warmth: .2)] } },
            "held" => state with { Held = true },
            "pause" => state with { Suspensions = [SuspensionReason.Pause] },
            "route" => state with { Observed = state.Observed with { LastResult = state.Observed.LastResult! with { Backend = "UnknownRoute" } } },
            "nativeSaturation" => state with { ManualProfile = Profile(true) with { GlobalColor = new(180, Vibrance: .5) } },
            _ => state
        };
        Assert.False(DisplayChangePolicy.CanPreserveColor(state, Sdr, Sdr));
    }
    private sealed class CountingBackend(bool native) : IColorBackend
    {
        public int Applies, Restores;
        public Task<ApplyResult> ApplyAsync(ColorSettings color, long revision, CancellationToken cancellationToken)
        { Applies++; return Task.FromResult(new ApplyResult(true, native ? "Magnification+NvidiaDvc" : "Magnification", null, "Injected fake", revision)); }
        public Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken)
        { Restores++; return Task.FromResult(new ApplyResult(true, "Fake", null, "Injected restore", 0)); }
    }
}
