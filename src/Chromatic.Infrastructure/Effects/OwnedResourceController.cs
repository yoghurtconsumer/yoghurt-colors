using System.Collections.Immutable;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;

namespace Chromatic.Infrastructure.Effects;

public sealed class OwnedResourceController(IRecoveryJournalStore journal, RecoverySnapshot identity,
    IWriteSafetyGate safety, Func<bool> writerLeaseHeld) : IDisposable
{
    private readonly SemaphoreSlim _serial = new(1, 1);
    private RecoverySnapshot _snapshot = identity;
    private readonly string _ownerSid = identity.UserSid;
    private readonly int _ownerSession = identity.SessionId;
    private bool _initialized;
    public RecoverySnapshot Snapshot => _snapshot;

    public async Task<ApplyResult> ApplyAsync(IResourceAdapter adapter, ResourceValue requested,
        WriteFence fence, CancellationToken cancellationToken) =>
        await ApplyComputedAsync(adapter, _ => requested, fence, cancellationToken);

    public async Task<ApplyResult> ApplyComputedAsync(IResourceAdapter adapter, Func<ResourceValue, ResourceValue> composeFromBaseline,
        WriteFence fence, CancellationToken cancellationToken)
    {
        await _serial.WaitAsync(cancellationToken);
        try
        {
            if (!writerLeaseHeld() || !safety.RecoveryReady || !safety.CanWrite(fence))
                return Failure("SafetyGate", "Recovery, writer lease or current revision/generation is unavailable.", fence.Revision);
            if (_snapshot.Status == JournalStatus.Conflict)
                return Failure("Conflict", "Ownership conflict requires local review; automatic writes remain blocked.", fence.Revision);
            if (!_initialized)
            {
                var previous = await journal.LoadAsync(cancellationToken);
                if (previous is not null && previous.Status != JournalStatus.Restored)
                    return Failure("RecoveryPending", "Review/recover the unfinished journal before acquiring resources.", fence.Revision);
                _initialized = true;
            }
            var descriptor = adapter.Descriptor;
            var owned = _snapshot.Resources.SingleOrDefault(r => Matches(r, descriptor));
            var actual = await adapter.ReadAsync(cancellationToken);
            if (owned is null)
            {
                if (!adapter.CanOwn(actual)) return Failure("ExistingFilter", "A non-neutral filter is already present; ownership was declined.", fence.Revision);
                owned = new(Guid.NewGuid(), descriptor.Kind, descriptor.Scope, descriptor.Display, actual, actual, fence.Generation);
            }
            else if (!IsOurValue(owned, actual))
            {
                _snapshot = _snapshot with { Status = JournalStatus.Conflict, TimestampUtc = DateTimeOffset.UtcNow };
                await journal.SaveAsync(_snapshot, cancellationToken);
                return Failure("ForeignWriter", "Resource changed outside this owner; no write was performed.", fence.Revision);
            }
            var desired = adapter.Canonicalize(composeFromBaseline(owned.Baseline));
            // Preserve last CONFIRMED value separately from the prospective write.
            // A crash before/after the setter can therefore recover either state.
            owned = owned with { PendingValue = desired, Generation = fence.Generation };
            Put(owned, JournalStatus.Prepared, fence.Generation);
            await journal.SaveAsync(_snapshot, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!writerLeaseHeld() || !safety.RecoveryReady || !safety.CanWrite(fence))
                return Failure("StaleRevision", "Command was superseded while its journal was being flushed.", fence.Revision);
            await adapter.WriteAsync(desired, cancellationToken,
                () => !cancellationToken.IsCancellationRequested && writerLeaseHeld() && safety.RecoveryReady && safety.CanWrite(fence));
            var readback = await adapter.ReadAsync(CancellationToken.None);
            if (!Equivalent(readback, desired)) return Failure("ReadbackMismatch", "Native result did not match; pending journal retained for recovery.", fence.Revision);
            Put(owned with { LastApplied = desired, PendingValue = null }, JournalStatus.Applied, fence.Generation);
            await journal.SaveAsync(_snapshot, CancellationToken.None);
            return new(true, descriptor.Kind.ToString(), null, "Write completed and read back; visual hardware qualification is separate.", fence.Revision);
        }
        catch (WriteFenceRejectedException error) { return Failure("StaleRevision", error.Message, fence.Revision); }
        catch (Exception error) when (error is not OperationCanceledException)
        { return Failure("ResourceFailure", error.Message, fence.Revision); }
        finally { _serial.Release(); }
    }

    public async Task<ApplyResult> RestoreAsync(IReadOnlyList<IResourceAdapter> adapters, long revision, CancellationToken cancellationToken)
    {
        await _serial.WaitAsync(cancellationToken);
        try
        {
            if (!writerLeaseHeld()) return Failure("OwnershipLost", "Writer lease is not held.", revision);
            if (_snapshot.Resources.IsEmpty) return new(true, "OwnedResources", null, "No resources were acquired.", revision);
            _snapshot = _snapshot with { Status = JournalStatus.Restoring, TimestampUtc = DateTimeOffset.UtcNow };
            await journal.SaveAsync(_snapshot, cancellationToken);
            foreach (var resource in _snapshot.Resources.Where(r => !r.PersistentUndo))
            {
                var matching = adapters.Where(a => Matches(resource, a.Descriptor)).ToArray();
                if (matching.Length != 1) return await Conflict("Display/resource identity is missing or ambiguous.", revision);
                var adapter = matching[0];
                var actual = await adapter.ReadAsync(cancellationToken);
                if (Equivalent(actual, resource.Baseline)) continue;
                if (!IsOurValue(resource, actual)) return await Conflict("Foreign resource value found; restoration declined.", revision);
                if (!writerLeaseHeld()) return Failure("OwnershipLost", "Writer lease was lost before restore.", revision);
                await adapter.WriteAsync(resource.Baseline, cancellationToken, () => writerLeaseHeld() && !cancellationToken.IsCancellationRequested);
                if (!Equivalent(await adapter.ReadAsync(cancellationToken), resource.Baseline))
                    return await Conflict("Baseline restore readback mismatch.", revision);
            }
            _snapshot = _snapshot with { Status = JournalStatus.Restored, TimestampUtc = DateTimeOffset.UtcNow,
                Resources = _snapshot.Resources.Select(r => r.PersistentUndo ? r : r with { LastApplied = r.Baseline, PendingValue = null }).ToImmutableArray() };
            await journal.SaveAsync(_snapshot, cancellationToken);
            return new(true, "OwnedResources", null, "Captured baselines restored exactly.", revision);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        { return Failure("RestoreFailure", error.Message, revision); }
        finally { _serial.Release(); }
    }
    // Only Recovery may adopt a persisted journal, after process death and lease acquisition.
    public void AdoptAfterAgentExit(RecoverySnapshot snapshot, bool verifiedAgentExited)
    {
        _serial.Wait();
        try
        {
            if (!verifiedAgentExited || !writerLeaseHeld() || _initialized || !_snapshot.Resources.IsEmpty)
                throw new InvalidOperationException("Recovery ownership preconditions are not satisfied.");
            if (snapshot.UserSid != _ownerSid || snapshot.SessionId != _ownerSession)
                throw new UnauthorizedAccessException("Recovery belongs to another user/session.");
            _snapshot = snapshot; _initialized = true;
        }
        finally { _serial.Release(); }
    }
    private void Put(OwnedResourceBaseline resource, JournalStatus status, long generation) => _snapshot = _snapshot with
    {
        Generation = generation, Status = status, TimestampUtc = DateTimeOffset.UtcNow,
        Resources = _snapshot.Resources.Where(r => r.OwnershipId != resource.OwnershipId).Append(resource).ToImmutableArray()
    };
    private async Task<ApplyResult> Conflict(string reason, long revision)
    {
        _snapshot = _snapshot with { Status = JournalStatus.Conflict, TimestampUtc = DateTimeOffset.UtcNow };
        await journal.SaveAsync(_snapshot, CancellationToken.None);
        return Failure("ForeignWriterOrRestoreConflict", reason, revision);
    }
    public static bool Equivalent(ResourceValue first, ResourceValue second) => first.Values.SequenceEqual(second.Values)
        && first.TopologyBase64 == second.TopologyBase64 && first.TopologyFormat == second.TopologyFormat;
    private static bool IsOurValue(OwnedResourceBaseline resource, ResourceValue actual) => Equivalent(actual, resource.LastApplied)
        || resource.PendingValue is not null && Equivalent(actual, resource.PendingValue);
    private static bool Matches(OwnedResourceBaseline resource, ResourceDescriptor descriptor) => resource.Kind == descriptor.Kind
        && resource.Scope == descriptor.Scope && (resource.Display is null ? descriptor.Display is null
            : descriptor.Display is not null && string.Equals(resource.Display.MonitorDevicePath, descriptor.Display.MonitorDevicePath, StringComparison.OrdinalIgnoreCase)
              && resource.Display.EdidSerial == descriptor.Display.EdidSerial);
    private static ApplyResult Failure(string code, string explanation, long revision) => new(false, "OwnedResources", code, explanation, revision);
    public void Dispose() => _serial.Dispose();
}
