using System.Text.Json;
using Chromatic.Core.Models;

namespace Chromatic.Infrastructure.Persistence;

public static class MigrationRunner
{
    public static byte[] ToCurrent(string schema, ReadOnlyMemory<byte> data)
    {
        using var source = JsonContract.Parse(data);
        var root = source.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var version) || !version.TryGetInt32(out var number))
            throw new ContractException("A schemaVersion is required.");
        if (number == 1) return data.ToArray();
        // The only historical format we explicitly support. No guessing or lossy migration.
        if (number != 0 || schema != "settings") throw new ContractException("Unsupported schema version; original file preserved.");
        var allowed = new HashSet<string> { "schemaVersion", "effectsEnabled", "color", "closeToTray" };
        if (root.EnumerateObject().Any(p => !allowed.Contains(p.Name)) || root.EnumerateObject().Count() != 4)
            throw new ContractException("Unknown/missing legacy fields.");
        var color = root.GetProperty("color");
        JsonContract.Validate("profile", JsonSerializer.SerializeToElement(new DisplayProfile
        {
            GlobalColor = color.Deserialize<ColorSettings>(JsonContract.Options) ?? throw new ContractException("Missing legacy color.")
        }, JsonContract.Options));
        // Validate the ORIGINAL color too, so missing constructor fields cannot bypass strictness.
        var migrated = new AppSettings
        {
            EffectsEnabled = root.GetProperty("effectsEnabled").GetBoolean(),
            GlobalColor = color.Deserialize<ColorSettings>(JsonContract.Options)!,
            CloseBehavior = root.GetProperty("closeToTray").GetBoolean() ? CloseBehavior.MinimizeToTray : CloseBehavior.ExitAndRestore
        };
        using var complete = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(migrated, JsonContract.Options));
        var legacyWithOriginalColor = System.Text.Json.Nodes.JsonNode.Parse(complete.RootElement.GetRawText())!;
        legacyWithOriginalColor["globalColor"] = System.Text.Json.Nodes.JsonNode.Parse(color.GetRawText());
        using var checkedDocument = JsonDocument.Parse(legacyWithOriginalColor.ToJsonString());
        JsonContract.Validate("settings", checkedDocument.RootElement);
        return JsonSerializer.SerializeToUtf8Bytes(migrated, JsonContract.Options);
    }
}
