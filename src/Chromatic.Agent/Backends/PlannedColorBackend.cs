using Chromatic.Core.Abstractions;
using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Effects;
using Chromatic.Native;

namespace Chromatic.Agent.Backends;

public sealed class PlannedColorBackend(OwnedResourceController ownership, IReadOnlyList<IResourceAdapter> resources,
    IResourceAdapter matrix, Func<EffectsState> state, OptionalServices services) : IColorBackend
{
    private bool _nativeFailed;
    private bool _gammaFailed;
    public async Task<ApplyResult> ApplyAsync(ColorSettings color, long revision, CancellationToken cancellationToken)
    {
        var requested = state();
        if (requested.Revision != revision) return new(false, "ColorPlan", "StaleRevision", "Superseded before planning.", revision);
        var fence = new WriteFence(revision, requested.Generation);
        var nonlinear = color.Gamma != 1 || color.Shadows != 0 || color.Highlights != 0;
        var monitorWork = requested.Effective.Monitors.Where(m => nonlinear || m.Gamma != 1 || m.Warmth != 0).ToArray();
        if ((nonlinear || monitorWork.Length > 0) && resources.Count(r => r.Descriptor.Kind == OwnedResourceKind.GammaRamp) > 1)
            return new(false, "ColorPlan", "SharedRampUnqualified", "Multiple active gamma outputs may share driver ramps. LUT writes are blocked until independent-output hardware qualification; use global affine controls or one active SDR output.", revision);
        if ((nonlinear || monitorWork.Length > 0) && (!services.GammaCompatibility || _gammaFailed || monitorWork.Length == 0))
            return new(false, "ColorPlan", "GammaUnavailable", "Nonlinear controls require opted-in, explicit SDR outputs; ignored/failed LUTs remain disabled for this session.", revision);
        if (requested.Effective.HoldModifier is not null && (nonlinear || monitorWork.Length > 0 ||
            services.NvidiaExperimental && color.Vibrance != 0 && color.SaturationPercent == 100 && requested.Effective.Monitors.Length == 1))
            return new(false, "ColorPlan", "HoldLatencyUnqualified", "Hold currently supports basic global affine lift only. Gamma/native DVC hold requires independent latency qualification.", revision);
        // Restore the departing plan before acquiring a different route. This also
        // detects a foreign writer before any new resource can be modified.
        var restore = await RestoreAsync(cancellationToken);
        if (!restore.Success) return restore with { Revision = revision };
        var softwareVibrance = true;
        var note = "Software matrix; global across every display.";
        if (!_nativeFailed && services.NvidiaExperimental && color.Vibrance != 0 && color.SaturationPercent == 100 && requested.Effective.Monitors.Length == 1)
        {
            var selected = requested.Effective.Monitors[0].Display;
            var native = resources.OfType<NvidiaDvcResource>().SingleOrDefault(r => r.Descriptor.Display == selected);
            if (native is not null)
            {
                var result = await new NvidiaDvcBackend(ownership, native, () => fence, resources).ApplyAsync(selected, color.Vibrance, revision, cancellationToken);
                if (result.Success) { softwareVibrance = false; note = "Experimental native DVC on the selected output; independent global affine/saturation matrix."; }
                else
                {
                    _nativeFailed = true;
                    restore = await RestoreAsync(CancellationToken.None);
                    if (!restore.Success) return restore with { Revision = revision };
                    note = $"Private DVC unavailable ({result.ErrorCode}); restored and using software vibrance approximation.";
                }
            }
        }
        if (color.DarkenStrength != 0 || color.HueRotationDegrees != 0)
            note += " Experimental global hue/colour darkening; other colours change too. No game element detection or verified black loot result.";
        var composed = ColorMatrixBuilder.Build(color, softwareVibrance);
        if (requested.Effective.HoldModifier is { } boost)
        {
            var tint = Matrix5x5.Identity.Values.ToArray(); tint[0] = 1 - boost.RedAttenuation;
            tint[6] = 1 - boost.GreenAttenuation; tint[12] = 1 - boost.BlueAttenuation;
            composed = composed * ColorMatrixBuilder.BasicLift(boost.Strength * .15) * new Matrix5x5(tint);
            note += " Temporary basic affine lift affects the entire image; it is not shadow-only.";
        }
        var applied = await ownership.ApplyAsync(matrix, new(composed.Values), fence, cancellationToken);
        if (!applied.Success) return applied;
        foreach (var monitor in monitorWork)
        {
            // Global and per-output gamma combine multiplicatively; reject out-of-range products.
            var gamma = monitor.Gamma * color.Gamma;
            if (gamma is < .5 or > 2.5) return new(false, "ColorPlan", "GammaRange", "Combined global/per-output gamma must be within 0.5–2.5.", revision);
            var result = await new SdrGammaBackend(ownership, resources, () => fence).ApplyTonesAsync(monitor with { Gamma = gamma },
                color.Shadows, color.Highlights, null, revision, cancellationToken);
            if (!result.Success) { _gammaFailed = true; return result; }
        }
        return new(true, softwareVibrance ? "Magnification" : "Magnification+NvidiaDvc", null,
            note + (color.CvdMode != "None" ? " CVD correction is an experimental encoded-SDR approximation." : "") +
            " API readback succeeded; visual/capture parity is unqualified.", revision);
    }
    public Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken) => ownership.RestoreAsync(resources, state().Revision, cancellationToken);
}
