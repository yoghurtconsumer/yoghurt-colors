using System.Runtime.InteropServices;
using Chromatic.Core.Color;

namespace Chromatic.Native;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct NativeGammaRamp
{
    public fixed ushort Red[256];
    public fixed ushort Green[256];
    public fixed ushort Blue[256];
    public static NativeGammaRamp FromRamp(GammaRamp ramp)
    {
        NativeGammaRamp result = default;
        for (var i = 0; i < 256; i++) { result.Red[i] = ramp.Values[i]; result.Green[i] = ramp.Values[256 + i]; result.Blue[i] = ramp.Values[512 + i]; }
        return result;
    }
    public GammaRamp ToRamp()
    {
        var values = new ushort[768];
        fixed (ushort* r = Red, g = Green, b = Blue)
            for (var i = 0; i < 256; i++) { values[i] = r[i]; values[256 + i] = g[i]; values[512 + i] = b[i]; }
        return new(values);
    }
}

public interface IGammaApi : IDisposable
{
    GammaRamp Read();
    void Write(GammaRamp ramp);
}
public sealed class GammaApi : IGammaApi
{
    private nint _dc;
    public GammaApi(string gdiName)
    {
        if (!RuntimeCapabilities.MeetsMinimum) throw new PlatformNotSupportedException();
        _dc = CreateDCW("DISPLAY", gdiName, 0, 0);
        if (_dc == 0) throw new InvalidOperationException("Could not open the selected display DC.");
    }
    public GammaRamp Read()
    {
        ObjectDisposedException.ThrowIf(_dc == 0, this);
        if (!GetDeviceGammaRamp(_dc, out var ramp)) throw new InvalidOperationException("GetDeviceGammaRamp failed.");
        return ramp.ToRamp();
    }
    public void Write(GammaRamp ramp)
    {
        ObjectDisposedException.ThrowIf(_dc == 0, this);
        var native = NativeGammaRamp.FromRamp(ramp);
        if (!SetDeviceGammaRamp(_dc, in native)) throw new InvalidOperationException("SetDeviceGammaRamp failed.");
    }
    public void Dispose() { var dc = Interlocked.Exchange(ref _dc, 0); if (dc != 0) _ = DeleteDC(dc); }
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint CreateDCW(string driver, string device, nint output, nint initData);
    [DllImport("gdi32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDeviceGammaRamp(nint dc, out NativeGammaRamp ramp);
    [DllImport("gdi32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDeviceGammaRamp(nint dc, in NativeGammaRamp ramp);
    [DllImport("gdi32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint dc);
}
