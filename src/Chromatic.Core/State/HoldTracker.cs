using System.Collections.Immutable;
using Chromatic.Core.Validation;
namespace Chromatic.Core.State;

public readonly record struct InputPacket(int Key, bool Down, bool Injected = false);
public sealed class HoldTracker
{
    private readonly HashSet<int> _keys;
    private readonly HashSet<int> _down = [], _blocked = [];
    private readonly bool _acceptInjected;
    public bool Held { get; private set; }
    public HoldTracker(ImmutableArray<int> keys, bool acceptInjected = false)
    { AutomationValidator.ValidateHold(keys); _keys = keys.ToHashSet(); _acceptInjected = acceptInjected; }
    public static int Normalize(int key) => key switch { 160 or 161 => 16, 162 or 163 => 17, 164 or 165 => 18, _ => key };
    public bool? Process(InputPacket packet, bool eligible)
    {
        var key = Normalize(packet.Key);
        if (!_keys.Contains(key) || packet.Injected && !_acceptInjected) return null;
        if (!packet.Down) { _down.Remove(key); _blocked.Remove(key); }
        else if (!eligible || _blocked.Contains(key)) { _blocked.Add(key); return null; }
        else if (!_down.Add(key)) return null;
        var next = eligible && _blocked.Count == 0 && _keys.All(_down.Contains);
        if (next == Held) return null;
        Held = next; return next;
    }
    public bool Reset()
    {
        foreach (var key in _down) _blocked.Add(key);
        _down.Clear(); var previous = Held; Held = false; return previous;
    }
    public void RequireFreshPress(Func<int, bool> isDown)
    { Reset(); foreach (var key in _keys) if (isDown(key)) _blocked.Add(key); }
    public bool Reconcile(Func<int, bool> isDown)
    {
        var missed = _down.Any(key => !isDown(key));
        foreach (var key in _blocked.ToArray()) if (!isDown(key)) _blocked.Remove(key);
        return missed && Reset(); // Polling can release, never activate.
    }
}
