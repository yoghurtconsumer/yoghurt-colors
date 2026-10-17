using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Chromatic.Core.Models;
using Chromatic.Core.Validation;
using Windows.Win32;
using Windows.Win32.Devices.Display;

namespace Chromatic.Native;

public sealed record DisplayBufferCounts(int Status, uint Paths, uint Modes);
public sealed record DisplayPathQuery(int Status, ImmutableArray<DisplayOutputInfo> Outputs);
public sealed record EnumeratedDisplayMode(uint Index, DisplayMode Mode, bool Interlaced, bool HasRequiredFields, ushort DriverExtra);
public interface IDisplayInventoryApi
{
    DisplayBufferCounts Counts();
    DisplayPathQuery Query(uint paths, uint modes);
    ImmutableArray<EnumeratedDisplayMode> Enumerate(string gdiName);
}
// Read-only adapter, injected in every automated test. No mode/HDR/DDC setter.
public sealed class DisplayInventoryReader(IDisplayInventoryApi api)
{
    public DisplayInventorySnapshot Read()
    {
        DisplayPathQuery Query()
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var sizes = api.Counts();
                if (sizes.Status != 0) throw new InvalidOperationException($"CCD buffer query status {sizes.Status}.");
                if (sizes.Paths is 0 or > 128 || sizes.Modes is 0 or > 512) throw new InvalidOperationException("CCD inventory exceeds bounded counts.");
                var result = api.Query(sizes.Paths, sizes.Modes);
                if (result.Status == 122) continue;
                if (result.Status != 0) throw new InvalidOperationException($"CCD query status {result.Status}.");
                if (result.Outputs.IsEmpty || result.Outputs.Length > sizes.Paths) throw new InvalidOperationException("Incomplete CCD inventory.");
                return result;
            }
            throw new InvalidOperationException("Display topology changed repeatedly. Refresh when stable.");
        }
        var captured = Query();
        if (captured.Outputs.Select(o => o.Display.MonitorDevicePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != captured.Outputs.Length)
            throw new InvalidOperationException("Ambiguous monitor device paths; no inventory plan available.");
        var choices = ImmutableDictionary.CreateBuilder<string, ImmutableArray<DisplayModeChoice>>(StringComparer.OrdinalIgnoreCase);
        var outputs = ImmutableArray.CreateBuilder<DisplayOutputInfo>();
        foreach (var raw in captured.Outputs)
        {
            SettingsValidator.ValidateDisplay(raw.Display); SettingsValidator.Text(raw.Name, 128, "monitor name");
            SettingsValidator.ValidateIntent(new(raw.Current.Width, raw.Current.Height, raw.Current.RefreshRate));
            var cloned = captured.Outputs.Count(o => o.SourceAdapterLuid == raw.SourceAdapterLuid && o.SourceId == raw.SourceId) > 1;
            var output = raw with { Cloned = cloned }; outputs.Add(output);
            var modes = ImmutableArray.CreateBuilder<DisplayModeChoice>();
            bool Valid(DisplayMode mode)
            {
                if (mode.BitsPerPixel != 32) return false;
                try { SettingsValidator.ValidateIntent(new(mode.Width, mode.Height, mode.RefreshRate)); return true; }
                catch (ArgumentException) { return false; }
            }
            if (!output.Interlaced && Valid(output.Current)) modes.Add(new("current", output.Current, true, "CCD current (observed)"));
            var enumerated = api.Enumerate(output.Display.GdiName);
            if (enumerated.Length > 2048) throw new InvalidOperationException("Mode enumeration exceeds its bound.");
            foreach (var mode in enumerated)
            {
                if (mode.Interlaced || !mode.HasRequiredFields || mode.DriverExtra != 0 || !Valid(mode.Mode)) continue;
                if (modes.Any(m => m.Mode == mode.Mode)) continue;
                modes.Add(new($"driver-{mode.Index}", mode.Mode, false, "Driver enumeration (integer Hz)"));
            }
            choices.Add(output.Display.MonitorDevicePath, modes.ToImmutable());
        }
        // Reject a topology change that occurred during the slower mode enumeration.
        if (!captured.Outputs.SequenceEqual(Query().Outputs)) throw new InvalidOperationException("Display mappings changed during enumeration; refresh again.");
        return new(Guid.NewGuid(), outputs.ToImmutable(), choices.ToImmutable(),
            "Read-only active topology and driver modes. No mode/HDR/scaling write or persistent custom timing was performed.");
    }
}

public sealed class WindowsDisplayInventoryApi : IDisplayInventoryApi
{
    private const QUERY_DISPLAY_CONFIG_FLAGS Flags = QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS;
    public DisplayBufferCounts Counts()
    {
        if (!RuntimeCapabilities.MeetsMinimum) throw new PlatformNotSupportedException("Windows 19041 x64 is required.");
        var result = PInvoke.GetDisplayConfigBufferSizes(Flags, out var paths, out var modes);
        return new((int)result, paths, modes);
    }
    public unsafe DisplayPathQuery Query(uint paths, uint modes)
    {
        if (paths is 0 or > 128 || modes is 0 or > 512) throw new ArgumentOutOfRangeException(nameof(paths));
        var pathBuffer = new DISPLAYCONFIG_PATH_INFO[paths]; var modeBuffer = new DISPLAYCONFIG_MODE_INFO[modes];
        var status = PInvoke.QueryDisplayConfig(Flags, ref paths, pathBuffer, ref modes, modeBuffer);
        if ((int)status != 0) return new((int)status, []);
        if (paths > pathBuffer.Length || modes > modeBuffer.Length) throw new InvalidOperationException("CCD returned invalid counts.");
        var outputs = ImmutableArray.CreateBuilder<DisplayOutputInfo>();
        for (var i = 0; i < paths; i++)
        {
            var path = pathBuffer[i];
            if (!path.targetInfo.targetAvailable || (path.flags & 1) == 0 || path.sourceInfo.modeInfoIdx >= modes)
                throw new InvalidOperationException("Unavailable target or missing source mode.");
            var sourceMode = modeBuffer[path.sourceInfo.modeInfoIdx];
            if ((uint)sourceMode.infoType != 1 || sourceMode.id != path.sourceInfo.id || Luid(sourceMode.adapterId) != Luid(path.sourceInfo.adapterId))
                throw new InvalidOperationException("CCD source index does not match its source identity.");
            DISPLAYCONFIG_SOURCE_DEVICE_NAME source = new(); source.header = Header(1, (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME), path.sourceInfo.adapterId, path.sourceInfo.id);
            DISPLAYCONFIG_TARGET_DEVICE_NAME target = new(); target.header = Header(2, (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME), path.targetInfo.adapterId, path.targetInfo.id);
            if (PInvoke.DisplayConfigGetDeviceInfo(&source.header) != 0 || PInvoke.DisplayConfigGetDeviceInfo(&target.header) != 0)
                throw new InvalidOperationException("Could not resolve monitor/source identity.");
            var display = new DisplayIdentity(target.monitorDevicePath.ToString(), null, source.viewGdiDeviceName.ToString(), Luid(path.targetInfo.adapterId), path.targetInfo.id);
            if (!display.GdiName.StartsWith(@"\\.\DISPLAY", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unmapped GDI source.");
            var color = Color(path.targetInfo.adapterId, path.targetInfo.id);
            var name = target.monitorFriendlyDeviceName.ToString(); if (string.IsNullOrWhiteSpace(name)) name = display.GdiName;
            var current = new DisplayMode(checked((int)sourceMode.sourceMode.width), checked((int)sourceMode.sourceMode.height),
                new(path.targetInfo.refreshRate.Numerator, path.targetInfo.refreshRate.Denominator), (uint)sourceMode.sourceMode.pixelFormat == 4 ? 32 : 0);
            outputs.Add(new(display, name, path.sourceInfo.id, Luid(path.sourceInfo.adapterId), sourceMode.sourceMode.position.x, sourceMode.sourceMode.position.y,
                current, (uint)path.targetInfo.rotation, (uint)path.targetInfo.scaling, false, (uint)path.targetInfo.scanLineOrdering is 2 or 3, color.Mode, color.Status));
        }
        return new(0, outputs.ToImmutable());
    }
    private static long Luid(Windows.Win32.Foundation.LUID value) => ((long)value.HighPart << 32) | value.LowPart;
    private static DISPLAYCONFIG_DEVICE_INFO_HEADER Header(uint type, uint size, Windows.Win32.Foundation.LUID adapter, uint id) =>
        new() { type = (DISPLAYCONFIG_DEVICE_INFO_TYPE)type, size = size, adapterId = adapter, id = id };
    private static unsafe (string Mode, string Status) Color(Windows.Win32.Foundation.LUID adapter, uint target)
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
        {
            DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 modern = new(); modern.header = Header(15, (uint)sizeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2), adapter, target);
            var status = PInvoke.DisplayConfigGetDeviceInfo(&modern.header);
            if (status == 0) return DescribeModernColor(modern.Anonymous.value, (uint)modern.activeColorMode);
            if (status is not (50 or 87)) return ("Unknown", $"ColorInfo2 status {status}; no HDR action.");
        }
        DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO legacy = new(); legacy.header = Header(9, (uint)sizeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO), adapter, target);
        var result = PInvoke.DisplayConfigGetDeviceInfo(&legacy.header);
        return result == 0 ? (legacy.advancedColorEnabled || legacy.wideColorEnforced ? "Legacy advanced color (HDR/WCG ambiguous)" : "SDR (legacy)",
            "Legacy advanced color is not unambiguous HDR capability. Use Windows settings; no HDR action.") : ("Unknown", $"Advanced-color query status {result}; no HDR action.");
    }
    public static (string Mode, string Status) DescribeModernColor(uint flags, uint mode) =>
        (mode switch { 0 => (flags & 2) == 0 ? "SDR" : "Advanced color active (mode SDR)", 1 => "WCG", 2 => "HDR", _ => "Unknown" },
        $"HDR supported={((flags & 16) != 0)}, user-enabled={((flags & 32) != 0)}, policy-limited={((flags & 8) != 0)}. Read-only; no HDR action.");
    public ImmutableArray<EnumeratedDisplayMode> Enumerate(string gdiName)
    {
        var current = new DisplayDevModeW { Size = 220, DriverExtra = 0 };
        if (!EnumDisplaySettingsExW(gdiName, uint.MaxValue, ref current, 0) || current.DriverExtra != 0)
            throw new InvalidOperationException("Current DEVMODE could not be captured without private driver bytes.");
        var result = ImmutableArray.CreateBuilder<EnumeratedDisplayMode>();
        for (uint index = 0; index <= 2048; index++)
        {
            var mode = new DisplayDevModeW { Size = 220, DriverExtra = 0 };
            if (!EnumDisplaySettingsExW(gdiName, index, ref mode, 0)) return result.ToImmutable();
            if (index == 2048) throw new InvalidOperationException("Driver mode enumeration did not end within its bound.");
            const uint required = 0x40000 | 0x80000 | 0x100000 | 0x200000 | 0x400000;
            result.Add(new(index, new(checked((int)mode.Width), checked((int)mode.Height), new(mode.Frequency, 1), checked((int)mode.BitsPerPel)),
                (mode.DisplayFlags & 2) != 0, (mode.Fields & required) == required, mode.DriverExtra));
        }
        throw new InvalidOperationException("Unreachable mode enumeration.");
    }
    public static void VerifyAbi()
    {
        DisplaySafetyProbe.VerifyAbi();
        if (Marshal.SizeOf<DisplayDevModeW>() != 220 || Marshal.OffsetOf<DisplayDevModeW>(nameof(DisplayDevModeW.Fields)).ToInt32() != 72 ||
            Marshal.OffsetOf<DisplayDevModeW>(nameof(DisplayDevModeW.Width)).ToInt32() != 172 || Marshal.OffsetOf<DisplayDevModeW>(nameof(DisplayDevModeW.Frequency)).ToInt32() != 184)
            throw new TypeLoadException("DEVMODEW display-arm ABI mismatch.");
    }
    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplaySettingsExW(string device, uint index, ref DisplayDevModeW mode, uint flags);
}
// Spec's display-only arm of the DEVMODEW union. No private driver bytes allocated.
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct DisplayDevModeW
{
    public fixed char DeviceName[32]; public ushort SpecVersion, DriverVersion, Size, DriverExtra;
    public uint Fields; public int X, Y; public uint Orientation, FixedOutput;
    public short Color, Duplex, YResolution, TTOption, Collate; public fixed char FormName[32]; public ushort LogPixels;
    public uint BitsPerPel, Width, Height, DisplayFlags, Frequency, ICMMethod, ICMIntent, MediaType, DitherType, Reserved1, Reserved2, PanningWidth, PanningHeight;
}
