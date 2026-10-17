using Chromatic.Core.Models;
using Chromatic.Core.State;
namespace Chromatic.Agent;

public enum AutomationSignalKind { Key, Hotkey, Foreground }
public readonly record struct AutomationSignal(AutomationSignalKind Kind, InputPacket Input = default, int HotkeyId = 0, nint Window = 0, uint EventTime = 0);
public sealed record ForegroundObservation(nint Window, ExecutableIdentity? Identity, string? MonitorGdi = null);
public interface IAutomationInput : IDisposable
{
    string Status { get; }
    bool Overflowed { get; }
    bool TryRead(out AutomationSignal signal);
    bool IsDown(int configuredKey);
    ForegroundObservation ObserveForeground();
}
