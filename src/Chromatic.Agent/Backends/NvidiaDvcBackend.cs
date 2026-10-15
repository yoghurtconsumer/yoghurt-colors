using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.Validation;
using Chromatic.Infrastructure.Effects;
using Chromatic.Native;
namespace Chromatic.Agent.Backends;
public sealed class NvidiaDvcBackend(OwnedResourceController? ownership = null, NvidiaDvcResource? resource = null,
    Func<WriteFence>? currentFence = null, IReadOnlyList<IResourceAdapter>? catalog = null) : IVibranceBackend
{
    public Task<ApplyResult> ApplyAsync(DisplayIdentity display, double normalizedVibrance, long revision, CancellationToken cancellationToken)
    {
        SettingsValidator.Range(normalizedVibrance, -1, 1, nameof(normalizedVibrance));
        if (ownership is null || resource is null || currentFence is null)
            return Task.FromResult(new ApplyResult(false, "NvidiaDvc", "SafetyGate", "Experimental output qualification, ownership and recovery are required.", revision));
        var target = resource.Descriptor.Display;
        if (target is null || display.MonitorDevicePath != target.MonitorDevicePath || display.EdidSerial != target.EdidSerial)
            return Task.FromResult(new ApplyResult(false, "NvidiaDvc", "UnsupportedDisplay", "Selected output does not match the qualified DVC resource.", revision));
        var fence = currentFence();
        if (fence.Revision != revision) return Task.FromResult(new ApplyResult(false, "NvidiaDvc", "StaleRevision", "Superseded before planning.", revision));
        return ownership.ApplyComputedAsync(resource, _ => resource.Desired(normalizedVibrance), fence, cancellationToken);
    }
    public Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken) => ownership is not null && resource is not null
        ? ownership.RestoreAsync(catalog ?? [resource], currentFence?.Invoke().Revision ?? 0, cancellationToken)
        : Task.FromResult(new ApplyResult(false, "NvidiaDvc", "SafetyGate", "No owned DVC backend is configured.", 0));
}
