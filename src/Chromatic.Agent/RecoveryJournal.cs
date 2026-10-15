using Chromatic.Core.Models;
using Chromatic.Core.Validation;
using Chromatic.Infrastructure.Persistence;
namespace Chromatic.Agent;
public sealed class RecoveryJournal(string path)
{
    private readonly AtomicJsonStore<RecoverySnapshot> _store = new(path, "recovery", RecoveryValidator.Validate,
        () => throw new ContractException("No journal exists; no baseline may be assumed."));
    public Task<LoadResult<RecoverySnapshot>> ReadAsync(CancellationToken cancellationToken = default) => _store.LoadAsync(cancellationToken);
    public Task WriteBeforeMutationAsync(RecoverySnapshot snapshot, CancellationToken cancellationToken = default) => _store.SaveAsync(snapshot, cancellationToken);
}
