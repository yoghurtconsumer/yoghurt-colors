using System.Runtime.InteropServices;
namespace Chromatic.Native;
public static class AbiAssertions
{
    public static void Verify()
    {
        if (!Environment.Is64BitProcess || Marshal.SizeOf<MagColorEffect>() != 100 || Marshal.SizeOf<NativeGammaRamp>() != 1536
            || Marshal.SizeOf<NvDisplayDvcInfo>() != 20 || Marshal.OffsetOf<NativeGammaRamp>(nameof(NativeGammaRamp.Green)).ToInt32() != 512
            || Marshal.OffsetOf<NativeGammaRamp>(nameof(NativeGammaRamp.Blue)).ToInt32() != 1024
            || Marshal.OffsetOf<NvDisplayDvcInfo>(nameof(NvDisplayDvcInfo.DefaultLevel)).ToInt32() != 16)
            throw new PlatformNotSupportedException("Native x64 color ABI verification failed.");
    }
}
