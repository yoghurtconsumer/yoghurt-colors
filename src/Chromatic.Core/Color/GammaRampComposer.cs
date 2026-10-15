using System.Buffers.Binary;
using System.Collections.Immutable;
using Chromatic.Core.Models;
using Chromatic.Core.Validation;

namespace Chromatic.Core.Color;

public sealed class GammaRamp
{
    // Three contiguous 256-entry WORD channels, never RGB-interleaved.
    public ImmutableArray<ushort> Values { get; }
    public GammaRamp(IEnumerable<ushort> values)
    {
        Values = values.ToImmutableArray();
        if (Values.Length != 768) throw new ArgumentException("A captured gamma ramp requires 768 WORDs.", nameof(values));
    }
    public ImmutableArray<byte> ToBytes()
    {
        var bytes = new byte[1536];
        for (var i = 0; i < Values.Length; i++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), Values[i]);
        return bytes.ToImmutableArray();
    }
    public static GammaRamp FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 1536) throw new ArgumentException("Gamma baseline must contain exactly 1536 bytes.", nameof(bytes));
        var values = new ushort[768];
        for (var i = 0; i < values.Length; i++) values[i] = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(i * 2)..]);
        return new(values);
    }
}

public static class GammaRampComposer
{
    public static GammaRamp Compose(GammaRamp capturedBaseline, double gamma = 1, double shadows = 0,
        double highlights = 0, double perMonitorWarmth = 0, HoldBoostSettings? hold = null)
    {
        SettingsValidator.Range(perMonitorWarmth, -1, 1, nameof(perMonitorWarmth));
        _ = ToneCurveBuilder.Map(0, gamma, shadows, highlights);
        if (hold is not null) SettingsValidator.Validate(new AppSettings { HoldBoost = hold });
        for (var channel = 0; channel < 3; channel++)
            for (var i = 1; i < 256; i++)
                if (capturedBaseline.Values[channel * 256 + i] < capturedBaseline.Values[channel * 256 + i - 1])
                    throw new ArgumentException("Non-monotone calibration baseline; composition is disabled.", nameof(capturedBaseline));
        var output = new ushort[768];
        var activeHold = hold is { Enabled: true } ? hold : null;
        double[] gains = [1 + .1 * perMonitorWarmth, 1, 1 - .1 * perMonitorWarmth];
        double[] tint = [activeHold?.RedAttenuation ?? 0, activeHold?.GreenAttenuation ?? 0, activeHold?.BlueAttenuation ?? 0];
        for (var channel = 0; channel < 3; channel++)
            for (var i = 0; i < 256; i++)
            {
                var mapped = ToneCurveBuilder.Map(i / 255d, gamma, shadows, highlights, activeHold?.Strength ?? 0);
                var position = Math.Clamp(mapped * gains[channel] * (1 - tint[channel]), 0, 1) * 255;
                var lower = (int)Math.Floor(position);
                var upper = Math.Min(255, lower + 1);
                var baseline = capturedBaseline.Values;
                var interpolated = baseline[channel * 256 + lower]
                    + (baseline[channel * 256 + upper] - baseline[channel * 256 + lower]) * (position - lower);
                output[channel * 256 + i] = (ushort)Math.Round(interpolated, MidpointRounding.AwayFromZero);
            }
        return new(output);
    }
}
