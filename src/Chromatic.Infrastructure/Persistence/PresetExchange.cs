using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Chromatic.Core.Models;
using Chromatic.Core.Validation;
namespace Chromatic.Infrastructure.Persistence;

public static class PresetExchange
{
    public const int MaxBytes = 65536, MaxCodeLength = 48000;
    public static byte[] Encode(PresetDocument preset)
    {
        PresetValidator.Validate(preset);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(preset, JsonContract.Options);
        _ = Decode(bytes); return bytes;
    }
    public static PresetDocument Decode(ReadOnlyMemory<byte> bytes)
    {
        using var doc = JsonContract.Parse(bytes, MaxBytes); JsonContract.Validate("preset", doc.RootElement);
        var preset = doc.RootElement.Deserialize<PresetDocument>(JsonContract.Options) ?? throw new ContractException("Missing preset.");
        PresetValidator.Validate(preset); return preset;
    }
    public static string Share(PresetDocument preset)
    {
        using var document = JsonDocument.Parse(Encode(preset));
        using var canonical = new MemoryStream();
        using (var writer = new Utf8JsonWriter(canonical)) WriteCanonical(writer, document.RootElement);
        var bytes = canonical.ToArray(); using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, true)) gzip.Write(bytes);
        var code = "CD1-" + Convert.ToBase64String(output.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_') + "." +
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (code.Length > MaxCodeLength) throw new ContractException("Preset is too large for an offline share code; export JSON instead.");
        return code;
    }
    // Typed serialization normalizes fields/numbers; ordinal keys and compact JSON
    // make the offline code independent of imported whitespace/property ordering.
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item); writer.WriteEndArray(); }
        else value.WriteTo(writer);
    }
    public static PresetDocument ImportCode(string code)
    {
        if (code.Length > MaxCodeLength || !code.StartsWith("CD1-", StringComparison.Ordinal)) throw new ContractException("Invalid Chromatic share code.");
        var parts = code[4..].Split('.');
        if (parts.Length != 2 || parts[0].Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_') || !SettingsValidator.Hash().IsMatch(parts[1]))
            throw new ContractException("Invalid code encoding/checksum.");
        try
        {
            var base64 = parts[0].Replace('-', '+').Replace('_', '/'); base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            using var input = new MemoryStream(Convert.FromBase64String(base64));
            using var gzip = new GZipStream(input, CompressionMode.Decompress); using var output = new MemoryStream();
            var buffer = new byte[4096]; int count;
            while ((count = gzip.Read(buffer)) > 0)
            { if (output.Length + count > MaxBytes) throw new ContractException("Decompressed preset exceeds 64 KiB."); output.Write(buffer, 0, count); }
            var bytes = output.ToArray();
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), Convert.FromHexString(parts[1]))) throw new ContractException("Preset checksum mismatch.");
            return Decode(bytes);
        }
        catch (Exception e) when (e is InvalidDataException or FormatException) { throw new ContractException("Invalid compressed preset."); }
    }
    public static PresetDocument ParseLink(string text)
    {
        if (text.Length > MaxCodeLength + 40 || !Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != "chromaticdesk" ||
            uri.Host != "preset" || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || !uri.IsDefaultPort ||
            uri.AbsolutePath.Length < 2 || uri.AbsolutePath[1..].Contains('/') || uri.AbsolutePath.Contains('%')) throw new ContractException("Expected chromaticdesk://preset/CD1-… without extra fields.");
        return ImportCode(uri.AbsolutePath[1..]);
    }
    public static async Task<PresetDocument> ReadFileAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaxBytes) throw new ContractException("Preset exceeds 64 KiB.");
        var bytes = new byte[checked((int)stream.Length)]; await stream.ReadExactlyAsync(bytes); return Decode(bytes);
    }
    public static Task WriteFileAsync(string path, PresetDocument preset) =>
        new AtomicJsonStore<PresetDocument>(path, "preset", PresetValidator.Validate, () => new()).SaveAsync(preset);
    public static string Diff(PresetDocument preset, ColorSettings current) =>
        $"Import '{preset.Name}' ({preset.Kind})\n" + (preset.Color is { } c ?
        $"Saturation {current.SaturationPercent:0} → {c.SaturationPercent:0}%; brightness {current.Brightness:0.00} → {c.Brightness:0.00}; contrast {current.Contrast:0.00} → {c.Contrast:0.00}; gamma {current.Gamma:0.00} → {c.Gamma:0.00}; warmth {current.Warmth:0.00} → {c.Warmth:0.00}; vibrance {current.Vibrance:0.00} → {c.Vibrance:0.00}; shadows {current.Shadows:0.00} → {c.Shadows:0.00}; highlights {current.Highlights:0.00} → {c.Highlights:0.00}; CVD {current.CvdMode}/{current.CvdStrength:0.00} → {c.CvdMode}/{c.CvdStrength:0.00}.\n" : "") +
        (preset.DisplayIntent is { } mode ? $"Pending display intent: {mode.Width}×{mode.Height}, {mode.RefreshRate.Numerator}/{mode.RefreshRate.Denominator} Hz, {mode.Scaling}, HDR={mode.Hdr}. Requires Stage 4 preview.\n" : "") +
        (preset.Crosshair is { } crosshair ? $"Crosshair enabled={crosshair.Enabled}, {crosshair.Shape}, size {crosshair.Size}, gap {crosshair.Gap}, thickness {crosshair.Thickness}, outline {crosshair.Outline}, color {crosshair.Color}, opacity {crosshair.Opacity}, offset {crosshair.OffsetX}/{crosshair.OffsetY}, asset {crosshair.AssetHash ?? "none"}. Requires Stage 5.\n" : "") +
        $"Game tag: {preset.GameTag ?? "none"}; author: {preset.AuthorMetadata?.Name ?? "unspecified"}; date: {preset.AuthorMetadata?.CreatedUtc.ToString("O") ?? "unspecified"}.\n" +
        $"Assets: {preset.Assets.Length}; hashes only, files are not supplied. Confirm adds a local copy; nothing is applied automatically. Author/checksum are not trust verification.";
}
