using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
namespace Chromatic.Agent.Backends;
public sealed class MagnificationBackend : IColorBackend
{
    public Task<ApplyResult> ApplyAsync(ColorSettings settings, long revision, CancellationToken cancellationToken) =>
        Task.FromResult(ApplyResult.NotImplemented("Magnification", revision));
    public Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken) => Task.FromResult(ApplyResult.NotImplemented("Magnification"));
}
