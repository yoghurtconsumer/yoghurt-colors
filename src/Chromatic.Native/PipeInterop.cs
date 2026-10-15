using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Chromatic.Native;

public sealed record NativePeerIdentity(int ProcessId, long StartTicks, int SessionId, string UserSid);
public static class PipeInterop
{
    public static NativePeerIdentity Current => ReadProcess(Environment.ProcessId);
    public static NamedPipeServerStream CreateCurrentUserLocalServer(string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^ChromaticDesktop\.\d+\.[a-f0-9]{32}$"))
            throw new ArgumentException("Invalid per-session pipe name.", nameof(name));
        using var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User?.Value ?? throw new UnauthorizedAccessException("No interactive user SID.");
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW($"D:P(A;;GA;;;{sid})", 1, out var descriptor, out _))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var attributes = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = descriptor };
            var handle = CreateNamedPipeW(@"\\.\pipe\" + name,
                3 | 0x40000000 | 0x00080000, // DUPLEX | OVERLAPPED | FIRST_PIPE_INSTANCE
                0x8, // BYTE type/read mode + PIPE_REJECT_REMOTE_CLIENTS, independent of DACL
                1, 65536, 65536, 5000, ref attributes);
            if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
            try { return new NamedPipeServerStream(PipeDirection.InOut, isAsync: true, isConnected: false, handle); }
            catch { handle.Dispose(); throw; }
        }
        finally { _ = LocalFree(descriptor); }
    }
    public static NativePeerIdentity ReadPeer(PipeStream pipe, bool serverSide)
    {
        uint pid;
        var success = serverSide ? GetNamedPipeClientProcessId(pipe.SafePipeHandle, out pid)
            : GetNamedPipeServerProcessId(pipe.SafePipeHandle, out pid);
        if (!success || pid == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        return ReadProcess(checked((int)pid));
    }
    public static NativePeerIdentity ReadProcess(int pid)
    {
        // Only OS metadata rights. Never PROCESS_VM_READ/WRITE or ALL_ACCESS.
        using var process = OpenProcess(0x1000, false, checked((uint)pid)); // QUERY_LIMITED_INFORMATION
        if (process.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        return ReadProcess(process, pid);
    }
    internal static NativePeerIdentity ReadProcess(SafeProcessHandle process, int pid)
    {
        if (!GetProcessTimes(process, out var created, out _, out _, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!OpenProcessToken(process, 0x8, out var token)) throw new Win32Exception(Marshal.GetLastWin32Error()); // TOKEN_QUERY
        using (token)
        {
            if (!GetTokenSessionId(token, 12, out var session, sizeof(uint), out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
            _ = GetTokenInformation(token, 1, 0, 0, out var length);
            if (length is 0 or > 65536) throw new UnauthorizedAccessException("Invalid token-user size.");
            var memory = Marshal.AllocHGlobal(checked((int)length));
            try
            {
                if (!GetTokenInformation(token, 1, memory, length, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
                var sid = new SecurityIdentifier(Marshal.ReadIntPtr(memory)).Value;
                return new(pid, DateTime.FromFileTimeUtc(created).Ticks, checked((int)session), sid);
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public nint Descriptor; [MarshalAs(UnmanagedType.Bool)] public bool Inherit; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern SafePipeHandle CreateNamedPipeW(string name, uint openMode, uint pipeMode, uint instances, uint outBuffer, uint inBuffer, uint timeout, ref SecurityAttributes attributes);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string text, uint revision, out nint descriptor, out uint size);
    [DllImport("kernel32.dll", ExactSpelling = true)] private static extern nint LocalFree(nint memory);
    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint pid);
    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint pid);
    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)] private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);
    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exited, out long kernel, out long user);
    [DllImport("advapi32.dll", SetLastError = true, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int type, nint info, uint length, out uint required);
    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenSessionId(SafeAccessTokenHandle token, int type, out uint session, uint length, out uint required);
}
