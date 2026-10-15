using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Chromatic.Native;

// Pins one OS process object, including its creation time, token and executable.
// Termination rights are requested only for our explicitly launched Agent executable.
public sealed class VerifiedProcess : IDisposable
{
    private readonly SafeProcessHandle _handle;
    private readonly bool _mayTerminate;
    public NativePeerIdentity Identity { get; }
    public VerifiedProcess(NativePeerIdentity expected, string executablePath, bool ownAgentMayTerminate = false)
    {
        var current = PipeInterop.Current;
        if (current.UserSid != expected.UserSid || current.SessionId != expected.SessionId)
            throw new UnauthorizedAccessException("Process is outside this interactive user/session.");
        if (ownAgentMayTerminate && !Path.GetFileName(executablePath).Equals("Chromatic.Agent.exe", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Only our Agent child can be terminated by recovery.");
        _handle = OpenProcess(0x1000 | 0x100000 | (ownAgentMayTerminate ? 1u : 0u), false, checked((uint)expected.ProcessId));
        try
        {
            if (_handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            Identity = PipeInterop.ReadProcess(_handle, expected.ProcessId);
            var image = new StringBuilder(32768); uint size = 32768;
            if (!QueryFullProcessImageNameW(_handle, 0, image, ref size)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (Identity != expected || !Path.GetFullPath(image.ToString()).Equals(Path.GetFullPath(executablePath), StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Launched process identity/image mismatch.");
            _mayTerminate = ownAgentMayTerminate;
        }
        catch { _handle.Dispose(); throw; }
    }
    public bool HasExited => WaitForSingleObject(_handle, 0) switch
    {
        0 => true, 258 => false, _ => throw new Win32Exception(Marshal.GetLastWin32Error())
    };
    public async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        while (!HasExited) await Task.Delay(100, cancellationToken);
    }
    public void TerminateHungAgent()
    {
        if (!_mayTerminate) throw new UnauthorizedAccessException("No Agent termination capability.");
        if (!HasExited && !TerminateProcess(_handle, 70)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public void Dispose() => _handle.Dispose();
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);
    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags, StringBuilder name, ref uint size);
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)] private static extern uint WaitForSingleObject(SafeProcessHandle process, uint milliseconds);
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(SafeProcessHandle process, uint code);
}
