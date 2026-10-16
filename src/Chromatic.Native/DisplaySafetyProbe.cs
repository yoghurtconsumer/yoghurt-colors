using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Chromatic.Core.Models;
using Windows.Win32;
using Windows.Win32.Devices.Display;

namespace Chromatic.Native;

public sealed record DisplayColorState(DisplayIdentity Display, string Api, uint Flags, string ActiveMode, uint BitsPerColorChannel);
public sealed record DisplaySafetyReport(ImmutableArray<DisplayIdentity> Displays, bool Known,
    bool AdvancedColorActive, string Reason)
{
    public ImmutableArray<DisplayColorState> ColorStates { get; init; } = [];
    public bool AllSdr => Known && !Displays.IsEmpty && !AdvancedColorActive;
    public bool Contains(DisplayIdentity display) => Known && Displays.Any(d => d == display);
}

// Read-only CCD inspection. Unknown, remote, cloned and changing mappings fail closed.
// No display configuration or HDR setter is requested/generated for this probe.
public static class DisplaySafetyProbe
{
    public static unsafe DisplaySafetyReport Read()
    {
        if (!RuntimeCapabilities.MeetsMinimum) return Unknown("Windows 19041 x64 is required.");
        const QUERY_DISPLAY_CONFIG_FLAGS flags = QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var status = PInvoke.GetDisplayConfigBufferSizes(flags, out var paths, out var modes);
            if ((uint)status != 0) return Unknown($"CCD buffer query failed: {(uint)status}.");
            if (paths is 0 or > 128 || modes > 512) return Unknown("CCD counts outside bounded inventory.");
            var pathBuffer = new DISPLAYCONFIG_PATH_INFO[paths];
            var modeBuffer = new DISPLAYCONFIG_MODE_INFO[modes];
            status = PInvoke.QueryDisplayConfig(flags, ref paths, pathBuffer, ref modes, modeBuffer);
            if ((uint)status == 122) continue;
            if ((uint)status != 0) return Unknown($"CCD topology query failed: {(uint)status}.");
            var displays = ImmutableArray.CreateBuilder<DisplayIdentity>();
            var colorStates = ImmutableArray.CreateBuilder<DisplayColorState>();
            var advanced = false;
            for (var i = 0; i < paths; i++)
            {
                var path = pathBuffer[i];
                DISPLAYCONFIG_SOURCE_DEVICE_NAME source = new();
                source.header = Header(DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                    (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME), path.sourceInfo.adapterId, path.sourceInfo.id);
                DISPLAYCONFIG_TARGET_DEVICE_NAME target = new();
                target.header = Header(DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                    (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME), path.targetInfo.adapterId, path.targetInfo.id);
                DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO color = new();
                color.header = Header(DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                    (uint)sizeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO), path.targetInfo.adapterId, path.targetInfo.id);
                if (PInvoke.DisplayConfigGetDeviceInfo(&source.header) != 0 ||
                    PInvoke.DisplayConfigGetDeviceInfo(&target.header) != 0 ||
                    PInvoke.DisplayConfigGetDeviceInfo(&color.header) != 0)
                    return Unknown("Display identity or advanced-color state could not be verified.");
                var devicePath = target.monitorDevicePath.ToString();
                var gdi = source.viewGdiDeviceName.ToString();
                if (string.IsNullOrWhiteSpace(devicePath) || !gdi.StartsWith(@"\\.\DISPLAY", StringComparison.OrdinalIgnoreCase))
                    return Unknown("No stable physical display mapping.");
                var luid = ((long)path.targetInfo.adapterId.HighPart << 32) | path.targetInfo.adapterId.LowPart;
                displays.Add(new(devicePath, null, gdi, luid, path.targetInfo.id));
                var identity = displays[^1];
                colorStates.Add(new(identity, "Legacy", color.Anonymous.value, "LegacyAdvancedColor", color.bitsPerColorChannel));
                var outputAdvanced = color.advancedColorEnabled || color.wideColorEnforced;
                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
                {
                    DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 modern = new();
                    modern.header = Header(DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2,
                        (uint)sizeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2), path.targetInfo.adapterId, path.targetInfo.id);
                    if (PInvoke.DisplayConfigGetDeviceInfo(&modern.header) != 0)
                        return Unknown("Current advanced-color mode could not be verified.");
                    colorStates.Add(new(identity, "Windows11ColorInfo2", modern.Anonymous.value, modern.activeColorMode.ToString(), modern.bitsPerColorChannel));
                    var activeMode = (uint)modern.activeColorMode;
                    if (activeMode > 2) return Unknown("Windows returned an unknown active color mode; effects remain blocked.");
                    // The modern packet's active mode is authoritative. On build
                    // 26200, legacy value=5 can accompany modern SDR value=69:
                    // legacy bit 2 alone must not override verified modern SDR.
                    outputAdvanced = IsModernAdvancedColor(modern.Anonymous.value, activeMode);
                }
                advanced |= outputAdvanced;
            }
            if (displays.Select(d => d.MonitorDevicePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != displays.Count ||
                displays.Select(d => d.GdiName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != displays.Count)
                return Unknown("Ambiguous or cloned display mapping; review topology before applying effects.");
            return new(displays.ToImmutable(), true, advanced,
                advanced ? "HDR/advanced color is active; SDR effects are blocked." : "Active outputs report SDR through read-only CCD.")
                { ColorStates = colorStates.ToImmutable() };
        }
        return Unknown("Display topology changed repeatedly during inventory.");
    }
    private static DisplaySafetyReport Unknown(string reason) => new([], false, false, reason);
    public static bool IsModernAdvancedColor(uint flags, uint activeMode)
    {
        if (activeMode > 2) throw new ArgumentOutOfRangeException(nameof(activeMode));
        // SDK: bit 1 is advancedColorActive; modes 0/1/2 are SDR/WCG/HDR.
        // Supported/user-enabled/reserved bits do not establish an active mode.
        return (flags & 2) != 0 || activeMode != 0;
    }
    private static DISPLAYCONFIG_DEVICE_INFO_HEADER Header(DISPLAYCONFIG_DEVICE_INFO_TYPE type, uint size,
        Windows.Win32.Foundation.LUID adapter, uint id) => new() { type = type, size = size, adapterId = adapter, id = id };

    public static void VerifyAbi()
    {
        Check<DISPLAYCONFIG_PATH_INFO>(72); Check<DISPLAYCONFIG_MODE_INFO>(64);
        Check<DISPLAYCONFIG_DEVICE_INFO_HEADER>(20); Check<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(32);
        Check<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2>(36);
        Check<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(84); Check<DISPLAYCONFIG_TARGET_DEVICE_NAME>(420);
    }
    private static void Check<T>(int expected) where T : struct
    {
        if (Marshal.SizeOf<T>() != expected) throw new TypeLoadException($"Windows SDK ABI mismatch: {typeof(T).Name}.");
    }
}
