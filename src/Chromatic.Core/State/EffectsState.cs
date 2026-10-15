using System.Collections.Immutable;
using Chromatic.Core.Models;

namespace Chromatic.Core.State;

public enum SuspensionReason { Pause, SessionLock, PowerSuspend, SessionDisconnected, AdvancedColor, Recovery }
public sealed record EffectiveEffects(bool Enabled, ColorSettings GlobalColor,
    ImmutableArray<MonitorColorSettings> Monitors, HoldBoostSettings? HoldModifier);
public sealed record ObservedEffects(long Revision, long Generation, ApplyResult? LastResult);

public sealed record EffectsState
{
    public long Revision { get; init; }
    public long Generation { get; init; }
    public bool Enabled { get; init; }
    public bool ShuttingDown { get; init; }
    public bool RecoveryPending { get; init; }
    public bool Held { get; init; }
    public DisplayProfile DesktopProfile { get; init; } = new();
    public DisplayProfile? GameProfile { get; init; }
    public DisplayProfile? ManualProfile { get; init; }
    public ExecutableIdentity? Foreground { get; init; }
    public HoldBoostSettings HoldBoost { get; init; } = new();
    public ImmutableHashSet<SuspensionReason> Suspensions { get; init; } = [];
    public PreviewTransaction? Preview { get; init; }
    public CapabilityReport Capabilities { get; init; } = CapabilityReport.Stage1;
    public ObservedEffects Observed { get; init; } = new(0, 0, null);
    public DisplayProfile BaseProfile => ManualProfile ?? GameProfile ?? DesktopProfile;
    public bool PreviewActive => Preview?.Status is PreviewStatus.Prepared or PreviewStatus.Applying
        or PreviewStatus.AwaitingConfirmation or PreviewStatus.RollingBack;
    public EffectiveEffects Effective => new(Enabled && !ShuttingDown && !RecoveryPending &&
        Suspensions.IsEmpty && !PreviewActive, BaseProfile.GlobalColor, BaseProfile.Monitors,
        Enabled && Held && HoldBoost.Enabled && !ShuttingDown && !RecoveryPending &&
        Suspensions.IsEmpty && !PreviewActive ? HoldBoost : null);
}
