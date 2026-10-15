using Chromatic.Core.Models;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Validation;
using Chromatic.Infrastructure.Effects;
namespace Chromatic.Recovery;
public sealed class RestoreExecutor
{
    private readonly IRecoveryJournalStore? _journal;
    private readonly IReadOnlyList<IResourceAdapter> _resources = [];
    private readonly Func<RecoverySnapshot, bool>? _agentExited;
    private readonly Func<bool>? _leaseHeld;
    private readonly string? _userSid;
    private readonly int _sessionId;
    public RestoreExecutor() { }
    public RestoreExecutor(IRecoveryJournalStore journal, IReadOnlyList<IResourceAdapter> resources,
        Func<RecoverySnapshot, bool> verifiedAgentExited, Func<bool> writerLeaseHeld, string currentUserSid, int currentSessionId)
    {
        _journal = journal; _resources = resources; _agentExited = verifiedAgentExited; _leaseHeld = writerLeaseHeld;
        _userSid = currentUserSid; _sessionId = currentSessionId;
    }
    public async Task<ApplyResult> RestoreAsync(RecoverySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (_journal is null || _agentExited is null || _leaseHeld is null)
            return ApplyResult.NotImplemented("Recovery"); // unconfigured instances can never mutate hardware
        RecoveryValidator.Validate(snapshot);
        if (snapshot.UserSid != _userSid || snapshot.SessionId != _sessionId)
            return new(false, "Recovery", "WrongSession", "Journal belongs to another user/session.", 0);
        if (!_agentExited(snapshot) || !_leaseHeld())
            return new(false, "Recovery", "AgentStillOwnsResources", "Verified Agent termination AND exclusive writer lease are required.", 0);
        if (snapshot.Status == JournalStatus.Conflict)
            return new(false, "Recovery", "Conflict", "Conflicting journal requires local review; no blind retries.", 0);
        using var controller = new OwnedResourceController(_journal, snapshot with { Resources = [] }, new RestoreOnlyGate(), _leaseHeld);
        controller.AdoptAfterAgentExit(snapshot, verifiedAgentExited: _agentExited(snapshot));
        return await controller.RestoreAsync(_resources, 0, cancellationToken);
    }
    private sealed class RestoreOnlyGate : IWriteSafetyGate
    {
        public bool RecoveryReady => true;
        public bool CanWrite(WriteFence fence) => false;
    }
}
