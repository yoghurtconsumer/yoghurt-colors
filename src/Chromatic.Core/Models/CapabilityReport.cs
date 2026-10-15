using System.Collections.Immutable;

namespace Chromatic.Core.Models;

public enum Capability { Available, Unsupported, NeedsUserAction, Experimental }
public enum BackendKind { Magnification, NvidiaDvc, SdrGamma, DisplayMode, Hdr, Ddc, Overlay, Media, Remote }
public enum EffectScope { GlobalDesktop, PerMonitor }
public sealed record BackendCapability(BackendKind Backend, Capability Status, string Reason,
    EffectScope Scope, DisplayIdentity? Display = null);
public sealed record CapabilityReport(ImmutableArray<BackendCapability> Backends, bool AdvancedColorActive = false)
{
    public static CapabilityReport Stage1 { get; } = new(Enum.GetValues<BackendKind>().Select(b =>
        new BackendCapability(b, Capability.Unsupported, "NotImplemented: Stage 1 foundation",
            b is BackendKind.SdrGamma or BackendKind.NvidiaDvc or BackendKind.DisplayMode or BackendKind.Hdr or BackendKind.Ddc
                ? EffectScope.PerMonitor : EffectScope.GlobalDesktop)).ToImmutableArray());
    public bool Supports(BackendKind kind, DisplayIdentity? display = null) => Backends.Any(b =>
        b.Backend == kind && b.Status == Capability.Available &&
        (b.Scope == EffectScope.GlobalDesktop || b.Display == display));
}
