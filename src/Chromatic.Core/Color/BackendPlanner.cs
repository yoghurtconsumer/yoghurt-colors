using System.Collections.Immutable;
using Chromatic.Core.Models;
using Chromatic.Core.State;

namespace Chromatic.Core.Color;

public sealed record BackendOperation(BackendKind Backend, EffectScope Scope, DisplayIdentity? Display);
public sealed record BackendPlan(ImmutableArray<BackendOperation> Operations, ImmutableArray<string> Limitations);

public static class BackendPlanner
{
    public static BackendPlan Plan(EffectsState state, OptionalServices services, bool captureFriendly = false,
        DisplayIdentity? selectedDisplay = null, bool allowNativeWithExtraSaturation = false)
    {
        var operations = ImmutableArray.CreateBuilder<BackendOperation>();
        var reasons = ImmutableArray.CreateBuilder<string>();
        if (!state.Effective.Enabled) return new([], ["Effects disabled, suspended, previewing or awaiting recovery."]);
        if (state.Capabilities.AdvancedColorActive)
            return new([], ["SDR effects suspended: HDR/advanced color is active on the desktop."]);
        var color = state.Effective.GlobalColor;
        var nativeVibrance = color.Vibrance != 0 && services.NvidiaExperimental && !captureFriendly
            && selectedDisplay is not null && state.Capabilities.Supports(BackendKind.NvidiaDvc, selectedDisplay)
            && (color.SaturationPercent == 100 || allowNativeWithExtraSaturation);
        // Saturation/accessibility always require cross-channel processing; gamma cannot substitute.
        if (color.SaturationPercent != 100 || color.Brightness != 0 || color.Contrast != 1 ||
            color.Warmth != 0 || color.CvdMode != "None" || color.Vibrance != 0 && !nativeVibrance)
            Add(BackendKind.Magnification, EffectScope.GlobalDesktop, null);
        if (color.Vibrance != 0)
        {
            if (nativeVibrance) Add(BackendKind.NvidiaDvc, EffectScope.PerMonitor, selectedDisplay);
            else reasons.Add("Software vibrance uses a saturation-like matrix approximation capped at 300%; native DVC requires a qualified selected output, opt-in and explicit permission when combined with extra saturation.");
        }
        var nonlinear = color.Gamma != 1 || color.Shadows != 0 || color.Highlights != 0 || state.Effective.HoldModifier is not null;
        var targets = state.Effective.Monitors.Select(m => m.Display).ToList();
        if (selectedDisplay is not null && !targets.Contains(selectedDisplay)) targets.Add(selectedDisplay);
        if (nonlinear && targets.Count == 0)
            reasons.Add("Nonlinear controls require explicit per-monitor targets; no shadow-only affine approximation is applied automatically.");
        if (nonlinear && (!services.GammaCompatibility || captureFriendly))
            reasons.Add("Nonlinear processing unavailable: SDR gamma opt-in is off or capture routing is active.");
        if (nonlinear && services.GammaCompatibility && !captureFriendly)
            foreach (var target in targets) Add(BackendKind.SdrGamma, EffectScope.PerMonitor, target);
        foreach (var monitor in state.Effective.Monitors)
        {
            if (monitor.Gamma == 1 && monitor.Warmth == 0) continue;
            if (services.GammaCompatibility && !captureFriendly)
                Add(BackendKind.SdrGamma, EffectScope.PerMonitor, monitor.Display);
            else reasons.Add($"Per-monitor gamma/warmth unavailable for {monitor.Display.GdiName}: compatibility opt-in required or capture route active.");
        }
        return new(operations.ToImmutable(), reasons.ToImmutable());

        void Add(BackendKind kind, EffectScope scope, DisplayIdentity? display)
        {
            if (operations.Any(o => o.Backend == kind && o.Display == display)) return;
            if (state.Capabilities.Supports(kind, display)) operations.Add(new(kind, scope, display));
            else reasons.Add($"{kind}: unavailable; {state.Capabilities.Backends.FirstOrDefault(b => b.Backend == kind)?.Reason ?? "capability not reported"}");
        }
    }
}
