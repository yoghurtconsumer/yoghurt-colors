using System.Runtime.InteropServices;
using Chromatic.Core.Color;

namespace Chromatic.Native;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct MagColorEffect
{
    public fixed float Transform[25];
    public static MagColorEffect FromMatrix(Matrix5x5 matrix)
    {
        var values = matrix.ToSinglePrecision();
        MagColorEffect result = default;
        for (var i = 0; i < 25; i++) result.Transform[i] = values[i];
        return result;
    }
    public Matrix5x5 ToMatrix()
    {
        var values = new double[25];
        fixed (float* data = Transform) for (var i = 0; i < 25; i++) values[i] = data[i];
        return new(values);
    }
}

public interface IMagnificationApi
{
    bool Initialize();
    bool Read(out MagColorEffect effect);
    bool Write(in MagColorEffect effect);
    bool Uninitialize();
}
public sealed class MagnificationApi : IMagnificationApi
{
    public bool Initialize() => RuntimeCapabilities.MeetsMinimum && MagInitialize();
    public bool Read(out MagColorEffect effect) => MagGetFullscreenColorEffect(out effect);
    public bool Write(in MagColorEffect effect) => MagSetFullscreenColorEffect(in effect);
    public bool Uninitialize() => MagUninitialize();
    // These BOOL APIs do not promise reliable extended last-error information.
    [DllImport("Magnification.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagInitialize();
    [DllImport("Magnification.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagGetFullscreenColorEffect(out MagColorEffect effect);
    [DllImport("Magnification.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagSetFullscreenColorEffect(in MagColorEffect effect);
    [DllImport("Magnification.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagUninitialize();
}
