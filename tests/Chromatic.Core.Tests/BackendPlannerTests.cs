using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Xunit;
namespace Chromatic.Core.Tests;
public sealed class BackendPlannerTests
{
    [Fact] public void UnavailableOrUnselectedNativeVibranceFallsBackToSoftware()
    {
        var caps = new CapabilityReport([new(BackendKind.Magnification, Capability.Available, "Fake", EffectScope.GlobalDesktop)]);
        var state = new EffectsState { Enabled = true, Capabilities = caps, ManualProfile = new() { GlobalColor = new(Vibrance: .5) } };
        Assert.Equal(BackendKind.Magnification, Assert.Single(BackendPlanner.Plan(state, new()).Operations).Backend);
        Assert.Equal(BackendKind.Magnification, Assert.Single(BackendPlanner.Plan(state, new(NvidiaExperimental: true)).Operations).Backend);
        Assert.Equal(BackendKind.Magnification, Assert.Single(BackendPlanner.Plan(state, new(), captureFriendly: true).Operations).Backend);
    }
    [Fact] public void CombinedNativeAndExtraSaturationRequiresExplicitAdvancedPermission()
    {
        var display = new DisplayIdentity("monitor", null, "DISPLAY1", 1, 1);
        var caps = new CapabilityReport([new(BackendKind.Magnification, Capability.Available, "Fake", EffectScope.GlobalDesktop),
            new(BackendKind.NvidiaDvc, Capability.Available, "Fake", EffectScope.PerMonitor, display)]);
        var state = new EffectsState { Enabled = true, Capabilities = caps, ManualProfile = new() { GlobalColor = new(150, Vibrance: .5) } };
        var services = new OptionalServices(NvidiaExperimental: true);
        var normal = BackendPlanner.Plan(state, services, selectedDisplay: display);
        Assert.Equal(BackendKind.Magnification, Assert.Single(normal.Operations).Backend);
        Assert.Equal(2, BackendPlanner.Plan(state, services, selectedDisplay: display, allowNativeWithExtraSaturation: true).Operations.Length);
    }
    [Fact] public void NonlinearAndNativeVibranceRequireAnExplicitCapableOutput()
    {
        var monitor = new DisplayIdentity("monitor-a", null, "DISPLAY1", 1, 1);
        var caps = new CapabilityReport([new(BackendKind.SdrGamma, Capability.Available, "Fake", EffectScope.PerMonitor, monitor),
            new(BackendKind.NvidiaDvc, Capability.Available, "Fake", EffectScope.PerMonitor, monitor)]);
        var state = new EffectsState { Enabled = true, Capabilities = caps, ManualProfile = new() { GlobalColor = new(Gamma: 1.2, Vibrance: .5) } };
        var preferences = new OptionalServices(GammaCompatibility: true, NvidiaExperimental: true);
        Assert.Empty(BackendPlanner.Plan(state, preferences).Operations);
        var planned = BackendPlanner.Plan(state, preferences, selectedDisplay: monitor);
        Assert.Equal(2, planned.Operations.Length); Assert.All(planned.Operations, o => Assert.Equal(monitor, o.Display));
        Assert.Empty(BackendPlanner.Plan(state, preferences, captureFriendly: true, selectedDisplay: monitor).Operations);
    }
    [Fact] public void DefaultPlanHasNoWrites() => Assert.Empty(BackendPlanner.Plan(new(), new()).Operations);
    [Fact] public void Stage1NeverClaimsNativeAvailability()
    {
        var state = new EffectsState { Enabled = true, ManualProfile = new() { GlobalColor = new(200) } };
        var plan = BackendPlanner.Plan(state, new());
        Assert.Empty(plan.Operations); Assert.Contains(plan.Limitations, x => x.Contains("NotImplemented"));
    }
    [Fact] public void GlobalSaturationIsNeverSubstitutedByGamma()
    {
        var monitor = new DisplayIdentity("monitor-a", null, "DISPLAY1", 1, 1);
        var caps = new CapabilityReport([new(BackendKind.SdrGamma, Capability.Available, "Fake only", EffectScope.PerMonitor, monitor)]);
        var state = new EffectsState { Enabled = true, Capabilities = caps, ManualProfile = new() { GlobalColor = new(200) } };
        Assert.Empty(BackendPlanner.Plan(state, new(GammaCompatibility: true)).Operations);
    }
    [Fact] public void GlobalMatrixAndPerMonitorWarmthHaveDistinctScopes()
    {
        var monitor = new DisplayIdentity("monitor-a", null, "DISPLAY1", 1, 1);
        var caps = new CapabilityReport([new(BackendKind.Magnification, Capability.Available, "Fake", EffectScope.GlobalDesktop),
            new(BackendKind.SdrGamma, Capability.Available, "Fake", EffectScope.PerMonitor, monitor)]);
        var state = new EffectsState { Enabled = true, Capabilities = caps, ManualProfile = new()
            { GlobalColor = new(150), Monitors = [new(monitor, Warmth: .5)] } };
        var operations = BackendPlanner.Plan(state, new(GammaCompatibility: true)).Operations;
        Assert.Equal(2, operations.Length); Assert.Null(operations[0].Display); Assert.Equal(monitor, operations[1].Display);
        Assert.Single(BackendPlanner.Plan(state, new(GammaCompatibility: true), true).Operations);
        Assert.Empty(BackendPlanner.Plan(state with { Capabilities = caps with { AdvancedColorActive = true } }, new(GammaCompatibility: true)).Operations);
    }
}
