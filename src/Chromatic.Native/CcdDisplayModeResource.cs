using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.Validation;
using Windows.Win32;
using Windows.Win32.Devices.Display;

namespace Chromatic.Native;

public sealed record CcdPacket(int Version, byte[] Paths, byte[] Modes, DisplayIdentity[] Displays);
// Captured SDK bytes never enter public IPC. This codec rejects malformed journals
// before any setter and keeps all active paths, modes, source positions and timing.
public static class CcdPacketCodec
{
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 };
    public static ResourceValue Encode(CcdPacket packet)
    {
        Validate(packet);
        return new([], Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(packet, Options)), "ccd-v1-x64");
    }
    public static CcdPacket Decode(ResourceValue value)
    {
        if (!value.Values.IsEmpty || value.TopologyFormat != "ccd-v1-x64" || value.TopologyBase64 is not { Length: > 0 and <= 524288 })
            throw new ArgumentException("Invalid captured CCD format.");
        var bytes = Convert.FromBase64String(value.TopologyBase64);
        if (bytes.Length > 262144) throw new ArgumentException("CCD payload exceeds bound.");
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
        RejectDuplicateFields(document.RootElement);
        var packet = JsonSerializer.Deserialize<CcdPacket>(bytes, Options) ?? throw new ArgumentException("Missing CCD packet.");
        Validate(packet); return packet;
    }
    private static void RejectDuplicateFields(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            { if (!names.Add(property.Name)) throw new ArgumentException("Duplicate CCD journal field."); RejectDuplicateFields(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) RejectDuplicateFields(item);
    }
    public static void Validate(CcdPacket packet)
    {
        WindowsDisplayInventoryApi.VerifyAbi();
        if (packet.Version != 1 || packet.Paths is null || packet.Modes is null || packet.Displays is null ||
            packet.Paths.Length is 0 or > 9216 || packet.Paths.Length % 72 != 0 || packet.Modes.Length is 0 or > 32768 || packet.Modes.Length % 64 != 0 ||
            packet.Displays.Length != packet.Paths.Length / 72) throw new ArgumentException("Invalid CCD counts/ABI.");
        var paths = MemoryMarshal.Cast<byte, DISPLAYCONFIG_PATH_INFO>(packet.Paths);
        var modes = MemoryMarshal.Cast<byte, DISPLAYCONFIG_MODE_INFO>(packet.Modes);
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < paths.Length; i++)
        {
            var path = paths[i]; var display = packet.Displays[i]; SettingsValidator.ValidateDisplay(display);
            if (!identities.Add(display.MonitorDevicePath) || !path.targetInfo.targetAvailable || (path.flags & 1) == 0 ||
                Luid(path.targetInfo.adapterId) != display.AdapterLuid || path.targetInfo.id != display.TargetId ||
                path.sourceInfo.modeInfoIdx >= modes.Length || path.targetInfo.modeInfoIdx >= modes.Length)
                throw new ArgumentException("Unmapped/invalid active CCD path.");
            var source = modes[(int)path.sourceInfo.modeInfoIdx]; var target = modes[(int)path.targetInfo.modeInfoIdx];
            if ((uint)source.infoType != 1 || source.id != path.sourceInfo.id || Luid(source.adapterId) != Luid(path.sourceInfo.adapterId) ||
                (uint)target.infoType != 2 || target.id != path.targetInfo.id || Luid(target.adapterId) != display.AdapterLuid)
                throw new ArgumentException("CCD mode index/identity mismatch.");
            SettingsValidator.ValidateIntent(new(checked((int)source.sourceMode.width), checked((int)source.sourceMode.height),
                new(path.targetInfo.refreshRate.Numerator, path.targetInfo.refreshRate.Denominator)));
            var signal = target.targetMode.targetVideoSignalInfo;
            if (signal.pixelRate == 0 || signal.activeSize.cx == 0 || signal.activeSize.cy == 0 ||
                signal.totalSize.cx < signal.activeSize.cx || signal.totalSize.cy < signal.activeSize.cy)
                throw new ArgumentException("Missing captured target timing.");
        }
    }
    internal static long Luid(Windows.Win32.Foundation.LUID value) => ((long)value.HighPart << 32) | value.LowPart;
    public static bool Equivalent(ResourceValue left, ResourceValue right) => Key(Decode(left)).SequenceEqual(Key(Decode(right)));
    private static byte[] Key(CcdPacket packet)
    {
        // Mode indexes/order, status and unused union padding can change on readback.
        // Compare the actual referenced source/target payload, complete timing,
        // priority order, identity and path policy rather than those bookkeeping bytes.
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output, Encoding.UTF8, true);
        var paths = MemoryMarshal.Cast<byte, DISPLAYCONFIG_PATH_INFO>(packet.Paths);
        foreach (var pair in packet.Displays.Select((d, i) => (d, i)))
        {
            var path = paths[pair.i]; writer.Write(pair.d.MonitorDevicePath.ToUpperInvariant()); writer.Write(pair.d.GdiName.ToUpperInvariant());
            writer.Write(packet.Paths.AsSpan(pair.i * 72, 12)); // adapter/source id
            writer.Write(packet.Paths.AsSpan(pair.i * 72 + 20, 12)); // adapter/target id
            writer.Write(packet.Paths.AsSpan(pair.i * 72 + 36, 28)); // output/scaling/rational/scan/available
            writer.Write(path.flags);
            writer.Write(packet.Modes.AsSpan(checked((int)path.sourceInfo.modeInfoIdx) * 64 + 16, 20));
            writer.Write(packet.Modes.AsSpan(checked((int)path.targetInfo.modeInfoIdx) * 64 + 16, 48));
        }
        return output.ToArray();
    }
    public static ResourceValue Plan(ResourceValue baseline, DisplayIdentity display, DisplayIntent intent)
    {
        SettingsValidator.ValidateIntent(intent); var packet = Decode(baseline);
        var paths = MemoryMarshal.Cast<byte, DISPLAYCONFIG_PATH_INFO>(packet.Paths); var modes = MemoryMarshal.Cast<byte, DISPLAYCONFIG_MODE_INFO>(packet.Modes);
        var index = Array.FindIndex(packet.Displays, d => d == display);
        if (index < 0 || intent.Hdr is not null) throw new NotSupportedException("Display mapping changed or HDR was requested; this preview is SDR source-mode only.");
        var path = paths[index]; var refresh = path.targetInfo.refreshRate;
        if ((ulong)refresh.Numerator * intent.RefreshRate.Denominator != (ulong)intent.RefreshRate.Numerator * refresh.Denominator)
            throw new NotSupportedException("Physical refresh changes require qualified captured timing. Choose a source-size option at the current target timing.");
        foreach (var other in paths)
            if (other.targetInfo.id != path.targetInfo.id || CcdPacketCodec.Luid(other.targetInfo.adapterId) != display.AdapterLuid)
                if (other.sourceInfo.id == path.sourceInfo.id && Luid(other.sourceInfo.adapterId) == Luid(path.sourceInfo.adapterId))
                    throw new NotSupportedException("Cloned shared sources cannot be previewed.");
        ref var source = ref modes[(int)path.sourceInfo.modeInfoIdx]; source.sourceMode.width = (uint)intent.Width; source.sourceMode.height = (uint)intent.Height;
        if (intent.Scaling != ScalingIntent.Unchanged)
            paths[index].targetInfo.scaling = (DISPLAYCONFIG_SCALING)(intent.Scaling switch { ScalingIntent.Centered => 2, ScalingIntent.Stretched => 3, ScalingIntent.AspectRatio => 4, _ => throw new ArgumentException("Unknown scaling") });
        return Encode(packet);
    }
}
public interface ICcdModeApi
{
    ResourceValue Capture();
    bool SourceSizeEnumerated(DisplayIdentity display, DisplayIntent intent);
    int Validate(ResourceValue value);
    int Apply(ResourceValue value);
}
public sealed class CcdDisplayModeResource(ICcdModeApi api) : IDisplayModeResource
{
    private ResourceValue? _lastRead;
    public ResourceDescriptor Descriptor { get; } = new(OwnedResourceKind.DisplayTopology, EffectScope.GlobalDesktop);
    public bool Equivalent(ResourceValue first, ResourceValue second) => CcdPacketCodec.Equivalent(first, second);
    public ResourceValue Canonicalize(ResourceValue value) { _ = CcdPacketCodec.Decode(value); return value; }
    public bool CanOwn(ResourceValue baseline) { _ = CcdPacketCodec.Decode(baseline); return true; }
    public Task<ResourceValue> ReadAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(_lastRead = api.Capture()); }
    public ResourceValue Plan(ResourceValue baseline, DisplayIdentity display, DisplayIntent intent)
    {
        if (!api.SourceSizeEnumerated(display, intent)) throw new NotSupportedException("Source size is not safely enumerated by this driver.");
        return CcdPacketCodec.Plan(baseline, display, intent);
    }
    public Task WriteAsync(ResourceValue value, CancellationToken token) => WriteAsync(value, token, () => true);
    public Task WriteAsync(ResourceValue value, CancellationToken token, Func<bool> authorized)
    {
        token.ThrowIfCancellationRequested(); Canonicalize(value);
        var before = _lastRead ?? throw new InvalidOperationException("CCD baseline/readback is required before writing.");
        var status = api.Validate(value); if (status != 0) throw new InvalidOperationException($"SDC_VALIDATE status {status}; no apply.");
        if (!Equivalent(api.Capture(), before)) throw new InvalidOperationException("Foreign topology changed during preflight; no apply.");
        if (!authorized()) throw new WriteFenceRejectedException();
        token.ThrowIfCancellationRequested(); status = api.Apply(value);
        if (status != 0) throw new InvalidOperationException($"SDC_APPLY status {status}.");
        return Task.CompletedTask;
    }
}
public sealed class WindowsCcdModeApi : ICcdModeApi
{
    public unsafe ResourceValue Capture()
    {
        if (!RuntimeCapabilities.MeetsMinimum) throw new PlatformNotSupportedException("Windows 19041 x64 required.");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var status = PInvoke.GetDisplayConfigBufferSizes(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, out var count, out var modeCount);
            if ((uint)status != 0 || count is 0 or > 128 || modeCount is 0 or > 512) throw new InvalidOperationException($"CCD capture buffer status {(uint)status}.");
            var paths = new DISPLAYCONFIG_PATH_INFO[count]; var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            status = PInvoke.QueryDisplayConfig(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, ref count, paths, ref modeCount, modes);
            if ((uint)status == 122) continue;
            if ((uint)status != 0) throw new InvalidOperationException($"CCD capture status {(uint)status}.");
            var displays = new List<DisplayIdentity>();
            for (var i = 0; i < count; i++)
            {
                var path = paths[i]; DISPLAYCONFIG_SOURCE_DEVICE_NAME source = new(); DISPLAYCONFIG_TARGET_DEVICE_NAME target = new();
                source.header = new() { type = (DISPLAYCONFIG_DEVICE_INFO_TYPE)1, size = (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME), adapterId = path.sourceInfo.adapterId, id = path.sourceInfo.id };
                target.header = new() { type = (DISPLAYCONFIG_DEVICE_INFO_TYPE)2, size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME), adapterId = path.targetInfo.adapterId, id = path.targetInfo.id };
                if (PInvoke.DisplayConfigGetDeviceInfo(&source.header) != 0 || PInvoke.DisplayConfigGetDeviceInfo(&target.header) != 0) throw new InvalidOperationException("CCD identity query failed.");
                displays.Add(new(target.monitorDevicePath.ToString(), null, source.viewGdiDeviceName.ToString(), CcdPacketCodec.Luid(path.targetInfo.adapterId), path.targetInfo.id));
            }
            var safety = DisplaySafetyProbe.Read();
            if (!safety.AllSdr || displays.Count != safety.Displays.Length || displays.Any(d => !safety.Contains(d))) throw new InvalidOperationException("Mode preview/restore requires unchanged verified SDR outputs; HDR/WCG is not toggled.");
            return CcdPacketCodec.Encode(new(1, MemoryMarshal.AsBytes(paths.AsSpan(0, (int)count)).ToArray(), MemoryMarshal.AsBytes(modes.AsSpan(0, (int)modeCount)).ToArray(), displays.ToArray()));
        }
        throw new InvalidOperationException("Topology changed repeatedly during capture.");
    }
    public bool SourceSizeEnumerated(DisplayIdentity display, DisplayIntent intent) => new WindowsDisplayInventoryApi().Enumerate(display.GdiName)
        .Any(m => !m.Interlaced && m.HasRequiredFields && m.DriverExtra == 0 && m.Mode.BitsPerPixel == 32 && m.Mode.Width == intent.Width && m.Mode.Height == intent.Height);
    private static unsafe int Set(ResourceValue value, uint flags)
    {
        var packet = CcdPacketCodec.Decode(value);
        var paths = MemoryMarshal.Cast<byte, DISPLAYCONFIG_PATH_INFO>(packet.Paths).ToArray(); var modes = MemoryMarshal.Cast<byte, DISPLAYCONFIG_MODE_INFO>(packet.Modes).ToArray();
        fixed (DISPLAYCONFIG_PATH_INFO* p = paths) fixed (DISPLAYCONFIG_MODE_INFO* m = modes)
            return (int)PInvoke.SetDisplayConfig((uint)paths.Length, p, (uint)modes.Length, m, (SET_DISPLAY_CONFIG_FLAGS)flags);
    }
    public int Validate(ResourceValue value) => Set(value, 0x20 | 0x40);
    public int Apply(ResourceValue value) => Set(value, 0x20 | 0x80); // No SAVE_TO_DATABASE / ALLOW_CHANGES.
}
