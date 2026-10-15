using Chromatic.Core.Models;
namespace Chromatic.Agent.Backends;
public sealed class HdrBackend
{
    public Task<ApplyResult> SetAsync(DisplayIdentity display, bool enabled, long revision, CancellationToken cancellationToken) =>
        Task.FromResult(ApplyResult.NotImplemented("Hdr", revision));
}
