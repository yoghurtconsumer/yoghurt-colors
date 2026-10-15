using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.Color;
using Chromatic.Infrastructure.Effects;
namespace Chromatic.Agent.Backends;
public sealed class MagnificationBackend(OwnedResourceController? ownership = null, IResourceAdapter? resource = null,
    Func<WriteFence>? currentFence = null, IReadOnlyList<IResourceAdapter>? catalog = null) : IColorBackend
{
    public Task<ApplyResult> ApplyAsync(ColorSettings settings, long revision, CancellationToken cancellationToken)
    {
        if (ownership is null || resource is null || currentFence is null) return Task.FromResult(Unavailable(revision));
        if (settings.Gamma != 1 || settings.Shadows != 0 || settings.Highlights != 0)
            return Task.FromResult(new ApplyResult(false, "Magnification", "NeedsGammaRoute", "Nonlinear controls need a qualified per-monitor LUT route.", revision));
        var fence = currentFence();
        if (fence.Revision != revision) return Task.FromResult(new ApplyResult(false, "Magnification", "StaleRevision", "Superseded before planning.", revision));
        return ownership.ApplyAsync(resource, new(ColorMatrixBuilder.Build(settings).Values), fence, cancellationToken);
    }
    public Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken) => ownership is not null && resource is not null
        ? ownership.RestoreAsync(catalog ?? [resource], currentFence?.Invoke().Revision ?? 0, cancellationToken)
        : Task.FromResult(Unavailable(0));
    private static ApplyResult Unavailable(long revision) => new(false, "Magnification", "SafetyGate", "An authenticated STA Agent, verified SDR and independent recovery are required.", revision);
}
