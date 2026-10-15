using Chromatic.Core.Models;

namespace Chromatic.Core.Abstractions;

public sealed record ResourceDescriptor(OwnedResourceKind Kind, EffectScope Scope, DisplayIdentity? Display = null);
public interface IResourceAdapter
{
    ResourceDescriptor Descriptor { get; }
    ResourceValue Canonicalize(ResourceValue value);
    bool CanOwn(ResourceValue baseline);
    Task<ResourceValue> ReadAsync(CancellationToken cancellationToken);
    Task WriteAsync(ResourceValue value, CancellationToken cancellationToken);
    // Adapters with a native preflight must recheck after that preflight, directly
    // before their setter. Simple injected resources use this default guard.
    Task WriteAsync(ResourceValue value, CancellationToken cancellationToken, Func<bool> stillAuthorized)
    {
        if (!stillAuthorized()) throw new WriteFenceRejectedException();
        return WriteAsync(value, cancellationToken);
    }
}
public sealed class WriteFenceRejectedException() : InvalidOperationException("Write ownership/revision changed during native preflight.");
public interface IRecoveryJournalStore
{
    Task<RecoverySnapshot?> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(RecoverySnapshot snapshot, CancellationToken cancellationToken);
}
public readonly record struct WriteFence(long Revision, long Generation);
public interface IWriteSafetyGate
{
    bool RecoveryReady { get; }
    bool CanWrite(WriteFence fence);
}
