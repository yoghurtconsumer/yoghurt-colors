using Chromatic.Core.Models;

namespace Chromatic.Core.State;

public static class ProfileResolver
{
    public static string? Resolve(ExecutableIdentity? foreground, IEnumerable<GameRule> rules)
    {
        if (foreground is null) return null;
        // Exact canonical paths only; the foreground adapter must normalize paths before dispatch.
        var matches = rules.Where(r => r.Enabled && string.Equals(r.ExecutablePath,
            foreground.NormalizedPath, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0].ProfileId : null;
    }
}
