using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
namespace Chromatic.Agent.Backends;
public sealed class DisplayModeBackend : IDisplayBackend
{
    public Task<ApplyResult> PreviewAsync(PreviewTransaction transaction, long revision, CancellationToken cancellationToken) =>
        Task.FromResult(ApplyResult.NotImplemented("DisplayMode", revision));
    public Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken) => Task.FromResult(ApplyResult.NotImplemented("DisplayMode"));
}
