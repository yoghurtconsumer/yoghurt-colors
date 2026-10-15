using System.Collections.Immutable;

namespace Chromatic.Core.Models;

public enum OwnedResourceKind { GlobalMatrix, GammaRamp, NativeVibrance, DisplayTopology, Hdr, TemporaryDdc }
public enum JournalStatus { Prepared, Applied, Restoring, Restored, Conflict, RecoveryRequired }
public enum PreviewStatus { Prepared, Applying, AwaitingConfirmation, Confirmed, RollingBack, Reverted, Failed }
public sealed record PreviewTransaction(Guid Id, PreviewStatus Status, DisplayIdentity Display,
    DisplayIntent Intent, DateTimeOffset DeadlineUtc, long Generation);

// Topology packets are bounded, version-tagged captured CCD bytes, never imported preset data.
// Stage 2/4 must validate ABI and capture complete paths/modes before constructing a journal.
public sealed record ResourceValue(ImmutableArray<double> Values, string? TopologyBase64 = null,
    string? TopologyFormat = null);
public sealed record OwnedResourceBaseline(Guid OwnershipId, OwnedResourceKind Kind,
    EffectScope Scope, DisplayIdentity? Display, ResourceValue Baseline, ResourceValue LastApplied,
    long Generation, bool PersistentUndo = false, ResourceValue? PendingValue = null);
public sealed record RecoverySnapshot
{
    public int SchemaVersion { get; init; } = 1;
    public Guid JournalId { get; init; } = Guid.NewGuid();
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public string UserSid { get; init; } = "";
    public int SessionId { get; init; }
    public int AgentPid { get; init; }
    public long AgentStartTicks { get; init; }
    public long Generation { get; init; }
    public JournalStatus Status { get; init; } = JournalStatus.Prepared;
    public ImmutableArray<OwnedResourceBaseline> Resources { get; init; } = [];
    public PreviewTransaction? Preview { get; init; }
}
