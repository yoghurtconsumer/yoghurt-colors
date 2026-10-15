using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
namespace Chromatic.Agent.Backends;
public sealed class DdcMonitorBackend : IMonitorBackend
{
    public Task<ApplyResult> SetAsync(DisplayIdentity display, MonitorControl control, uint value, long revision, CancellationToken cancellationToken) =>
        Task.FromResult(ApplyResult.NotImplemented("Ddc", revision));
    public Task<ApplyResult> UndoAsync(CancellationToken cancellationToken) => Task.FromResult(ApplyResult.NotImplemented("Ddc"));
}
