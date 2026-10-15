using System.Runtime.InteropServices;
using Chromatic.Core.Abstractions;

namespace Chromatic.Native;

[StructLayout(LayoutKind.Sequential)]
public struct NvDisplayDvcInfo
{
    public const uint Version1 = 20u | (1u << 16);
    public uint Version;
    public int CurrentLevel, MinLevel, MaxLevel, DefaultLevel;
    public readonly bool IsValid => Version == Version1 && MinLevel <= DefaultLevel && DefaultLevel <= MaxLevel
        && CurrentLevel >= MinLevel && CurrentLevel <= MaxLevel;
    public readonly int Map(double slider)
    {
        if (!IsValid || !double.IsFinite(slider) || slider is < 0 or > 1) throw new ArgumentException("Invalid DVC range or slider.");
        var level = slider < .5 ? MinLevel + 2 * slider * ((double)DefaultLevel - MinLevel)
            : DefaultLevel + (2 * slider - 1) * ((double)MaxLevel - DefaultLevel);
        return (int)Math.Clamp(Math.Round(level, MidpointRounding.AwayFromZero), MinLevel, MaxLevel);
    }
}

// Private ABI IDs/signatures are pinned to the independently documented wrapper
// cited in TECHNICAL-SPECIFICATION.md §3.3. No exported SetDVCLevelEx import.
public interface INvApi : IDisposable
{
    nint ResolveDisplay(string gdiName);
    NvDisplayDvcInfo Read(nint display);
    void Write(nint display, int level, Func<bool>? stillAuthorized = null);
}
public sealed class NvApi : INvApi
{
    private nint _module;
    private readonly NvUnload _unload;
    private readonly NvAssociatedDisplay _associated;
    private readonly NvGetDvc _get;
    private readonly NvSetDvc _set;
    public NvApi()
    {
        if (!RuntimeCapabilities.MeetsMinimum) throw new PlatformNotSupportedException();
        _module = LoadLibraryExW("nvapi64.dll", 0, 0x800); // LOAD_LIBRARY_SEARCH_SYSTEM32 only
        if (_module == 0) throw new NotSupportedException("Installed System32 nvapi64.dll is unavailable.");
        try
        {
            var entry = GetProcAddress(_module, "nvapi_QueryInterface");
            if (entry == 0) throw new NotSupportedException("nvapi_QueryInterface is unavailable.");
            var query = Marshal.GetDelegateForFunctionPointer<NvQueryInterface>(entry);
            T Resolve<T>(uint id) where T : Delegate
            {
                var pointer = query(id);
                if (pointer == 0) throw new NotSupportedException($"Required NvAPI interface {id:X8} is unavailable.");
                return Marshal.GetDelegateForFunctionPointer<T>(pointer);
            }
            var initialize = Resolve<NvInitialize>(0x0150E828);
            _unload = Resolve<NvUnload>(0xD22BDD7E);
            _associated = Resolve<NvAssociatedDisplay>(0x35C29134);
            _get = Resolve<NvGetDvc>(0x0E45002D); _set = Resolve<NvSetDvc>(0x4A82C2B1);
            Check(initialize(), "initialize");
        }
        catch { _ = FreeLibrary(_module); _module = 0; throw; }
    }
    public nint ResolveDisplay(string gdiName)
    {
        ObjectDisposedException.ThrowIf(_module == 0, this);
        Check(_associated(gdiName, out var handle), "display mapping");
        return handle != 0 ? handle : throw new NotSupportedException("Display-driving NVIDIA output not found.");
    }
    public NvDisplayDvcInfo Read(nint display)
    {
        ObjectDisposedException.ThrowIf(_module == 0, this);
        var info = new NvDisplayDvcInfo { Version = NvDisplayDvcInfo.Version1 };
        Check(_get(display, 0, ref info), "read DVC");
        return info.IsValid ? info : throw new NotSupportedException("Private DVC structure/ranges are incompatible.");
    }
    public void Write(nint display, int level, Func<bool>? stillAuthorized = null)
    {
        var info = Read(display);
        if (level < info.MinLevel || level > info.MaxLevel) throw new ArgumentOutOfRangeException(nameof(level));
        info.CurrentLevel = level;
        if (stillAuthorized?.Invoke() == false) throw new WriteFenceRejectedException();
        Check(_set(display, 0, ref info), "set DVC");
        if (Read(display).CurrentLevel != level) throw new InvalidOperationException("DVC readback mismatch.");
    }
    private static void Check(int status, string operation)
    { if (status != 0) throw new NotSupportedException($"NvAPI {operation} failed with status {status}; adapter disabled, no blind retries."); }
    public void Dispose()
    {
        var module = Interlocked.Exchange(ref _module, 0);
        if (module == 0) return;
        try { _ = _unload(); } finally { _ = FreeLibrary(module); }
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint NvQueryInterface(uint id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvInitialize();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvUnload();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private delegate int NvAssociatedDisplay([MarshalAs(UnmanagedType.LPStr)] string name, out nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvGetDvc(nint display, uint output, ref NvDisplayDvcInfo info);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvSetDvc(nint display, uint output, ref NvDisplayDvcInfo info);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern nint LoadLibraryExW(string file, nint reserved, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)] private static extern nint GetProcAddress(nint module, string name);
    [DllImport("kernel32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FreeLibrary(nint module);
}
