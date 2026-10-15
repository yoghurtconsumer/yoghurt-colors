using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.Validation;

namespace Chromatic.Infrastructure.Persistence;

public sealed class RecoveryJournalStore(string path) : IRecoveryJournalStore
{
    private readonly AtomicJsonStore<RecoverySnapshot> _store = new(path, "recovery", RecoveryValidator.Validate,
        () => throw new ContractException("No recovery baseline exists."));
    public async Task<RecoverySnapshot?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path) && !File.Exists(path + ".last-good")) return null;
        var result = await _store.LoadAsync(cancellationToken);
        if (result.Origin != LoadOrigin.Primary)
            throw new ContractException("Recovery journal needs manual review: automatic recovery from an older backup could omit the last owned resource.");
        return result.Value;
    }
    public Task SaveAsync(RecoverySnapshot snapshot, CancellationToken cancellationToken) => _store.SaveAsync(snapshot, cancellationToken);
}
