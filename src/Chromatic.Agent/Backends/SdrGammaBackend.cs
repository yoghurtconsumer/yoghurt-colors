using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
namespace Chromatic.Agent.Backends;
public sealed class SdrGammaBackend : IGammaBackend
{
    public Task<ApplyResult> ApplyAsync(MonitorColorSettings settings, HoldBoostSettings? hold, long revision, CancellationToken cancellationToken) =>
        Task.FromResult(ApplyResult.NotImplemented("SdrGamma", revision));
    public Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken) => Task.FromResult(ApplyResult.NotImplemented("SdrGamma"));
}
