using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Chromatic.Agent;

public enum AgentBroadcast { DisplayChanged, SessionSuspended, SessionUnlocked, PowerSuspended, PowerResumed }
public sealed class HiddenBroadcastWindow : IDisposable
{
    private readonly HwndSource _window;
    public event Action<AgentBroadcast>? Broadcast;
    public HiddenBroadcastWindow()
    {
        // Parent 0 is a hidden top-level window; HWND_MESSAGE would miss broadcasts.
        _window = new HwndSource(new HwndSourceParameters("Chromatic effects broadcasts")
        { ParentWindow = 0, WindowStyle = 0, Width = 0, Height = 0 });
        _window.AddHook(WndProc);
        if (!WTSRegisterSessionNotification(_window.Handle, 0))
        { _window.Dispose(); throw new InvalidOperationException("Session notification registration failed."); }
    }
    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        AgentBroadcast? broadcast = message switch
        {
            0x007E => AgentBroadcast.DisplayChanged,
            0x0218 when wParam == 4 => AgentBroadcast.PowerSuspended,
            0x0218 when wParam == 0x12 || wParam == 7 => AgentBroadcast.PowerResumed,
            0x02B1 when wParam == 7 || wParam == 2 || wParam == 4 => AgentBroadcast.SessionSuspended,
            0x02B1 when wParam == 8 || wParam == 1 || wParam == 3 => AgentBroadcast.SessionUnlocked,
            _ => null
        };
        if (broadcast is not null)
            _ = _window.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => Broadcast?.Invoke(broadcast.Value)));
        return 0;
    }
    public void Dispose()
    {
        _ = WTSUnRegisterSessionNotification(_window.Handle);
        _window.RemoveHook(WndProc); _window.Dispose();
    }
    [DllImport("wtsapi32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSRegisterSessionNotification(nint window, uint flags);
    [DllImport("wtsapi32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSUnRegisterSessionNotification(nint window);
}
