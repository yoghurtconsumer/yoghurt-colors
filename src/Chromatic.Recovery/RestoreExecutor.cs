using Chromatic.Core.Models;
namespace Chromatic.Recovery;
public sealed class RestoreExecutor
{
    public Task<ApplyResult> RestoreAsync(RecoverySnapshot snapshot, CancellationToken cancellationToken) =>
        Task.FromResult(ApplyResult.NotImplemented("Recovery"));
}
