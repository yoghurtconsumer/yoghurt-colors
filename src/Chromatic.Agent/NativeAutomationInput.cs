using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Windows.Interop;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Microsoft.Win32.SafeHandles;

namespace Chromatic.Agent;

// Only constructed after explicit opt-in. Every callback executes in this
// process, extracts bounded configured data and chains input without suppression.
public sealed class NativeAutomationInput : IAutomationInput
{
    private readonly AgentStaWorker _pump = new();
    private readonly Channel<AutomationSignal> _queue = Channel.CreateBounded<AutomationSignal>(new BoundedChannelOptions(64)
    { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly HashSet<int> _holdKeys;
    private readonly HashSet<int> _registered = [];
    private readonly HookProc _keyboard, _mouse;
    private readonly WinEventProc _foreground;
    private HwndSource? _window;
    private nint _keyboardHook, _mouseHook, _eventHook;
    private int _overflow, _disposed;
    private readonly bool _watchForeground;
    public string Status { get; private set; } = "Listeners not installed.";
    public bool Overflowed => Interlocked.Exchange(ref _overflow, 0) != 0;
    public NativeAutomationInput(AutomationSettings settings)
    {
        _holdKeys = settings.Boost.Enabled ? settings.HoldKeys.ToHashSet() : [];
        _watchForeground = settings.AutomaticProfiles || settings.Boost.Enabled;
        _keyboard = Keyboard; _mouse = Mouse; _foreground = (_, _, hwnd, _, _, _, time) =>
        { try { Enqueue(new(AutomationSignalKind.Foreground, Window: hwnd, EventTime: time)); } catch { Interlocked.Exchange(ref _overflow, 1); } };
        try { _pump.InvokeAsync(() => { Install(settings); return true; }).GetAwaiter().GetResult(); }
        catch { Dispose(); throw; }
    }
    private void Install(AutomationSettings settings)
    {
        var errors = new List<string>();
        _window = new HwndSource(new HwndSourceParameters("Chromatic input messages") { ParentWindow = 0, WindowStyle = 0, Width = 0, Height = 0 });
        _window.AddHook(Messages);
        if (settings.Boost.Enabled)
        {
            _keyboardHook = SetWindowsHookExW(13, _keyboard, GetModuleHandleW(null), 0);
            if (_keyboardHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Keyboard hold listener failed.");
            if (settings.MouseHolds)
            {
                _mouseHook = SetWindowsHookExW(14, _mouse, GetModuleHandleW(null), 0);
                if (_mouseHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Mouse hold listener failed.");
            }
        }
        if (_watchForeground)
        {
            _eventHook = SetWinEventHook(3, 3, 0, _foreground, 0, 0, 0); // EVENT_SYSTEM_FOREGROUND, OUTOFCONTEXT
            if (_eventHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Foreground listener failed.");
        }
        if (settings.HotkeysEnabled)
            foreach (var key in settings.Hotkeys)
            {
                if (RegisterHotKey(_window.Handle, key.Id, key.Modifiers | 0x4000, (uint)key.Key)) _registered.Add(key.Id);
                else errors.Add($"Hotkey {key.Id} conflict/error {Marshal.GetLastWin32Error()}");
            }
        Status = $"Listeners ready. Hotkeys {_registered.Count}/{(settings.HotkeysEnabled ? settings.Hotkeys.Length : 0)}; " +
            (settings.Boost.Enabled ? "basic affine hold opted in; " : "hold off; ") +
            (settings.AutomaticProfiles ? "automatic profiles opted in." : "automatic profiles off.") +
            (errors.Count == 0 ? "" : " " + string.Join("; ", errors));
    }
    private void Enqueue(AutomationSignal signal) { if (!_queue.Writer.TryWrite(signal)) Interlocked.Exchange(ref _overflow, 1); }
    private nint Keyboard(int code, nuint message, nint data)
    {
        try
        {
            if (code >= 0 && message is 0x100 or 0x104 or 0x101 or 0x105)
            {
                var key = Marshal.ReadInt32(data);
                if (_holdKeys.Contains(HoldTracker.Normalize(key)))
                    Enqueue(new(AutomationSignalKind.Key, new(key, message is 0x100 or 0x104, (Marshal.ReadInt32(data, 8) & 0x12) != 0)));
            }
        }
        catch { Interlocked.Exchange(ref _overflow, 1); }
        return CallNextHookEx(0, code, message, data);
    }
    private nint Mouse(int code, nuint message, nint data)
    {
        try
        {
            if (code >= 0 && message is 0x20B or 0x20C)
            {
                var button = (uint)Marshal.ReadInt32(data, 8) >> 16; var key = button == 1 ? 5 : button == 2 ? 6 : 0;
                if (_holdKeys.Contains(key)) Enqueue(new(AutomationSignalKind.Key,
                    new(key, message == 0x20B, (Marshal.ReadInt32(data, 12) & 3) != 0)));
            }
        }
        catch { Interlocked.Exchange(ref _overflow, 1); }
        return CallNextHookEx(0, code, message, data);
    }
    private nint Messages(nint hwnd, int message, nint wp, nint lp, ref bool handled)
    {
        try { if (message == 0x312 && _registered.Contains((int)wp)) Enqueue(new(AutomationSignalKind.Hotkey, HotkeyId: (int)wp)); }
        catch { Interlocked.Exchange(ref _overflow, 1); }
        return 0;
    }
    public bool TryRead(out AutomationSignal signal) => _queue.Reader.TryRead(out signal);
    public bool IsDown(int key) => _holdKeys.Contains(key) && (GetAsyncKeyState(key) & 0x8000) != 0;
    public ForegroundObservation ObserveForeground()
    {
        if (!_watchForeground) return new(0, null);
        var window = GetForegroundWindow();
        if (window == 0) return new(0, null);
        _ = GetWindowThreadProcessId(window, out var pid);
        if (pid == 0) return new(window, null);
        using var process = OpenProcess(0x1000, false, pid); // QUERY_LIMITED_INFORMATION, never VM rights
        if (process.IsInvalid) return new(window, null);
        var path = new StringBuilder(32768); uint length = (uint)path.Capacity;
        if (!QueryFullProcessImageNameW(process, 0, path, ref length) || !GetProcessTimes(process, out var created, out _, out _, out _)) return new(window, null);
        if (GetForegroundWindow() != window) return new(GetForegroundWindow(), null);
        _ = GetWindowThreadProcessId(window, out var currentPid);
        if (currentPid != pid) return new(window, null);
        try
        {
            var canonical = Path.GetFullPath(path.ToString()).Replace('/', '\\');
            var monitor = MonitorFromWindow(window, 2); var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), Device = "" };
            var gdi = monitor != 0 && GetMonitorInfoW(monitor, ref info) ? info.Device : null;
            return new(window, new(canonical, checked((int)pid), DateTime.FromFileTimeUtc(created).Ticks), gdi);
        }
        catch (Exception e) when (e is ArgumentException or OverflowException) { return new(window, null); }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _pump.InvokeAsync(() =>
        {
            if (_keyboardHook != 0) { _ = UnhookWindowsHookEx(_keyboardHook); _keyboardHook = 0; }
            if (_mouseHook != 0) { _ = UnhookWindowsHookEx(_mouseHook); _mouseHook = 0; }
            if (_eventHook != 0) { _ = UnhookWinEvent(_eventHook); _eventHook = 0; }
            if (_window is not null) { foreach (var key in _registered) _ = UnregisterHotKey(_window.Handle, key); _registered.Clear(); _window.RemoveHook(Messages); _window.Dispose(); _window = null; }
            _queue.Writer.TryComplete(); return true;
        }, true).GetAwaiter().GetResult();
        _pump.Dispose(); GC.KeepAlive(_keyboard); GC.KeepAlive(_mouse); GC.KeepAlive(_foreground);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MonitorInfo
    { public int Size; public Rect Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint HookProc(int code, nuint wp, nint lp);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void WinEventProc(nint hook, uint type, nint hwnd, int obj, int child, uint thread, uint time);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] private static extern nint SetWindowsHookExW(int id, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint CallNextHookEx(nint hook, int code, nuint wp, nint lp);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", ExactSpelling = true)] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);
    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)] private static extern nint GetModuleHandleW(string? name);
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint rights, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint process);
    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags, StringBuilder path, ref uint length);
    [DllImport("kernel32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exit, out long kernel, out long user);
}
