using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
namespace Chromatic.Agent.Backends;
public sealed class NvidiaDvcBackend : IVibranceBackend
{
    public Task<ApplyResult> ApplyAsync(DisplayIdentity display, double normalizedVibrance, long revision, CancellationToken cancellationToken) =>
        Task.FromResult(ApplyResult.NotImplemented("NvidiaDvc", revision));
    public Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken) => Task.FromResult(ApplyResult.NotImplemented("NvidiaDvc"));
}
