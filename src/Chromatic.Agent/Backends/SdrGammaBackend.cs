using System.Collections.Immutable;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.Color;
using Chromatic.Infrastructure.Effects;
namespace Chromatic.Agent.Backends;
public sealed class SdrGammaBackend(OwnedResourceController? ownership = null, IReadOnlyList<IResourceAdapter>? catalog = null,
    Func<WriteFence>? currentFence = null, Func<bool>? holdLatencyQualified = null) : IGammaBackend
{
    public Task<ApplyResult> ApplyAsync(MonitorColorSettings settings, HoldBoostSettings? hold, long revision, CancellationToken cancellationToken) =>
        ApplyTonesAsync(settings, 0, 0, hold, revision, cancellationToken);
    public async Task<ApplyResult> ApplyTonesAsync(MonitorColorSettings settings, double shadows, double highlights,
        HoldBoostSettings? hold, long revision, CancellationToken cancellationToken)
    {
        if (ownership is null || catalog is null || currentFence is null)
            return new(false, "SdrGamma", "SafetyGate", "Writer ownership/recovery and qualified SDR output mapping are required.", revision);
        if (hold is { Enabled: true } && holdLatencyQualified?.Invoke() != true)
            return new(false, "SdrGamma", "HoldLatencyUnqualified", "Hold boost requires measured gamma write latency qualification.", revision);
        var matches = catalog.Where(r => r.Descriptor.Kind == OwnedResourceKind.GammaRamp
            && r.Descriptor.Display is { } target && target.MonitorDevicePath == settings.Display.MonitorDevicePath
            && target.EdidSerial == settings.Display.EdidSerial).ToArray();
        if (matches.Length != 1) return new(false, "SdrGamma", "UnsupportedDisplay", "Gamma output mapping is missing or ambiguous.", revision);
        var fence = currentFence();
        if (fence.Revision != revision) return new(false, "SdrGamma", "StaleRevision", "Superseded before planning.", revision);
        var result = await ownership.ApplyComputedAsync(matches[0], baseline => new(GammaRampComposer.Compose(
            new(baseline.Values.Select(v => checked((ushort)v))), settings.Gamma, shadows, highlights,
            settings.Warmth, hold).Values.Select(v => (double)v).ToImmutableArray()), fence, cancellationToken);
        return result.Success && matches[0] is Chromatic.Native.GammaResource { LastWriteDuration: { } duration } && duration > TimeSpan.FromMilliseconds(250)
            ? new(false, "SdrGamma", "GammaSlow", "Gamma write exceeded 250 ms; restore the captured baseline and disable the LUT route for this session.", revision)
            : result;
    }
    public Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken) => ownership is not null && catalog is not null
        ? ownership.RestoreAsync(catalog, currentFence?.Invoke().Revision ?? 0, cancellationToken)
        : Task.FromResult(new ApplyResult(false, "SdrGamma", "SafetyGate", "No owned gamma backend is configured.", 0));
}
