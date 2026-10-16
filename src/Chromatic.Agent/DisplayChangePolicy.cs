using Chromatic.Core.State;
using Chromatic.Native;

namespace Chromatic.Agent;

// A resolution/refresh notification does not by itself invalidate a global
// matrix or native DVC. Preserve only an already confirmed SDR route on the exact
// same output mappings. Anything uncertain takes the existing restore path.
public static class DisplayChangePolicy
{
    public static bool CanPreserveColor(EffectsState state, DisplaySafetyReport previous, DisplaySafetyReport current)
    {
        if (!previous.AllSdr || !current.AllSdr || previous.Displays.Length != current.Displays.Length ||
            previous.Displays.Any(d => current.Displays.Count(next => next == d) != 1)) return false;
        var effective = state.Effective;
        var observed = state.Observed;
        if (!effective.Enabled || state.Held || effective.HoldModifier is not null ||
            observed.Revision != state.Revision || observed.Generation != state.Generation ||
            observed.LastResult is not { Success: true } result || result.Revision != state.Revision)
            return false;
        var color = effective.GlobalColor;
        var route = result.Backend == "Magnification" || result.Backend == "Magnification+NvidiaDvc" &&
            color.SaturationPercent == 100 && color.Vibrance != 0 && effective.Monitors.Length == 1;
        return route && color.Gamma == 1 && color.Shadows == 0 && color.Highlights == 0 &&
            effective.Monitors.All(m => m.Gamma == 1 && m.Warmth == 0);
    }
}
