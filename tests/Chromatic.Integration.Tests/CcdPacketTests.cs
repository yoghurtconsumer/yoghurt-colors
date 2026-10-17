using System.Buffers.Binary;
using Chromatic.Core.Models;
using Chromatic.Native;
using Chromatic.Core.Abstractions;
using Xunit;
namespace Chromatic.Integration.Tests;
public sealed class CcdPacketTests
{
    private static DisplayIdentity Display(int i) => new("captured-monitor-" + i, null, @"\\.\DISPLAY" + (i + 1), 1, (uint)(9 + i));
    private static CcdPacket Packet()
    {
        var paths = new byte[144]; var modes = new byte[256];
        void P(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(paths.AsSpan(offset), value);
        void M(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(modes.AsSpan(offset), value);
        for (var i = 0; i < 2; i++)
        {
            var p = i * 72; var m = i * 128;
            P(p, 1); P(p + 8, (uint)(2 + i)); P(p + 12, (uint)(i * 2)); P(p + 20, 1); P(p + 28, (uint)(9 + i)); P(p + 32, (uint)(i * 2 + 1));
            P(p + 36, 5); P(p + 40, 1); P(p + 44, 1); P(p + 48, 144); P(p + 52, 1); P(p + 56, 1); P(p + 60, 1); P(p + 68, 1);
            M(m, 1); M(m + 4, (uint)(2 + i)); M(m + 8, 1); M(m + 16, 1920); M(m + 20, 1080); M(m + 24, 4); M(m + 28, unchecked((uint)(i * 1920 - 1920)));
            m += 64; M(m, 2); M(m + 4, (uint)(9 + i)); M(m + 8, 1);
            BinaryPrimitives.WriteUInt64LittleEndian(modes.AsSpan(m + 16), 356400000);
            M(m + 24, 162000); M(m + 28, 1); M(m + 32, 144); M(m + 36, 1); M(m + 40, 1920); M(m + 44, 1080); M(m + 48, 2200); M(m + 52, 1125); M(m + 60, 1);
        }
        return new(1, paths, modes, [Display(0), Display(1)]);
    }
    private sealed class Api(ResourceValue baseline) : ICcdModeApi
    {
        public ResourceValue Actual = baseline; public int Applies, ValidationStatus; public Action? OnValidate;
        public ResourceValue Capture() => Actual;
        public bool SourceSizeEnumerated(DisplayIdentity display, DisplayIntent intent) => true;
        public int Validate(ResourceValue value) { OnValidate?.Invoke(); return ValidationStatus; }
        public int Apply(ResourceValue value) { Applies++; Actual = value; return 0; }
    }
    [Theory] [InlineData("fence")] [InlineData("foreign")] [InlineData("validate")]
    public async Task PreflightChangesAndValidationFailuresNeverReachApply(string failure)
    {
        var baseline = CcdPacketCodec.Encode(Packet()); var api = new Api(baseline); var resource = new CcdDisplayModeResource(api);
        await resource.ReadAsync(default); var desired = resource.Plan(baseline, Display(0), new(1280, 960, new(144, 1)));
        var authorized = true;
        api.OnValidate = () => { if (failure == "fence") authorized = false; if (failure == "foreign") api.Actual = desired; };
        if (failure == "validate") api.ValidationStatus = 87;
        await Assert.ThrowsAnyAsync<Exception>(() => resource.WriteAsync(desired, default, () => authorized));
        Assert.Equal(0, api.Applies);
    }
    [Fact] public void PlanChangesOnlySelectedSourceAndScalingPreservingTimingAndOtherMonitor()
    {
        var baseline = CcdPacketCodec.Encode(Packet()); var plan = CcdPacketCodec.Plan(baseline, Display(0), new(1280, 960, new(144, 1), ScalingIntent.Stretched));
        var first = CcdPacketCodec.Decode(baseline); var second = CcdPacketCodec.Decode(plan);
        Assert.Equal(first.Paths.AsSpan(72).ToArray(), second.Paths.AsSpan(72).ToArray());
        Assert.Equal(first.Modes.AsSpan(64).ToArray(), second.Modes.AsSpan(64).ToArray());
        Assert.Equal(1280u, BinaryPrimitives.ReadUInt32LittleEndian(second.Modes.AsSpan(16))); Assert.Equal(960u, BinaryPrimitives.ReadUInt32LittleEndian(second.Modes.AsSpan(20)));
        Assert.Equal(first.Modes.AsSpan(28, 8).ToArray(), second.Modes.AsSpan(28, 8).ToArray());
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(second.Paths.AsSpan(44)));
        Assert.False(CcdPacketCodec.Equivalent(baseline, plan));
    }
    [Fact] public void RefreshHdrAndUnmappedTargetsFailBeforeAnyNativeCall()
    {
        var baseline = CcdPacketCodec.Encode(Packet());
        Assert.Throws<NotSupportedException>(() => CcdPacketCodec.Plan(baseline, Display(0), new(1280, 960, new(60, 1))));
        Assert.Throws<NotSupportedException>(() => CcdPacketCodec.Plan(baseline, Display(0), new(1280, 960, new(144, 1), Hdr: true)));
        Assert.Throws<NotSupportedException>(() => CcdPacketCodec.Plan(baseline, Display(0) with { TargetId = 88 }, new(1280, 960, new(144, 1))));
    }
    [Theory] [InlineData("size")] [InlineData("version")] [InlineData("index")] [InlineData("identity")] [InlineData("timing")]
    public void CorruptCapturedPacketsAreRejectedBeforeAnySetter(string kind)
    {
        var packet = Packet();
        if (kind == "size") packet = packet with { Modes = new byte[1] };
        if (kind == "version") packet = packet with { Version = 2 };
        if (kind == "index") BinaryPrimitives.WriteUInt32LittleEndian(packet.Paths.AsSpan(12), uint.MaxValue);
        if (kind == "identity") packet.Displays[0] = Display(0) with { TargetId = 99 };
        if (kind == "timing") packet.Modes.AsSpan(80, 8).Clear();
        Assert.Throws<ArgumentException>(() => CcdPacketCodec.Encode(packet));
    }
    [Fact] public void ReadbackCanReorderModeIndicesButCannotChangeUnrelatedSourcePosition()
    {
        var packet = Packet(); var baseline = CcdPacketCodec.Encode(packet);
        packet = packet with { Modes = packet.Modes.AsSpan(128).ToArray().Concat(packet.Modes.AsSpan(0, 128).ToArray()).ToArray() };
        for (var i = 0; i < 2; i++) { BinaryPrimitives.WriteUInt32LittleEndian(packet.Paths.AsSpan(i * 72 + 12), (uint)((1 - i) * 2)); BinaryPrimitives.WriteUInt32LittleEndian(packet.Paths.AsSpan(i * 72 + 32), (uint)((1 - i) * 2 + 1)); }
        Assert.True(CcdPacketCodec.Equivalent(baseline, CcdPacketCodec.Encode(packet)));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.Modes.AsSpan(28), 50);
        Assert.False(CcdPacketCodec.Equivalent(baseline, CcdPacketCodec.Encode(packet)));
    }
}
