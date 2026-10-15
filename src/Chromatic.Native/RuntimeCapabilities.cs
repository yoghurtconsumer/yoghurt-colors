namespace Chromatic.Native;
public static class RuntimeCapabilities
{
    public static bool MeetsMinimum => Environment.Is64BitProcess && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);
    public static bool IsAtLeastBuild(int build) => MeetsMinimum && OperatingSystem.IsWindowsVersionAtLeast(10, 0, build);
}
