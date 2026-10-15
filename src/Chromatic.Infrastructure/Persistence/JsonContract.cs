using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Chromatic.Infrastructure.Persistence;

public sealed class ContractException(string message) : Exception(message);

public static class JsonContract
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 32, WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
    public static JsonDocument Parse(ReadOnlyMemory<byte> utf8, int maxBytes = 1_048_576)
    {
        if (utf8.Length == 0 || utf8.Length > maxBytes) throw new ContractException("Document exceeds its size bound or is empty.");
        var document = JsonDocument.Parse(utf8, new JsonDocumentOptions { MaxDepth = 32 });
        try { CheckUniqueKeys(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }
    private static void CheckUniqueKeys(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new ContractException($"Duplicate JSON field: {property.Name}");
                CheckUniqueKeys(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) CheckUniqueKeys(item);
    }
    public static void Validate(string schemaName, JsonElement value)
    {
        using var stream = typeof(JsonContract).Assembly.GetManifestResourceStream($"Chromatic.Schemas.{schemaName}.schema.json")
            ?? throw new ContractException($"Unknown schema {schemaName}.");
        using var schema = JsonDocument.Parse(stream);
        ValidateNode(schema.RootElement, schema.RootElement, value, "$");
    }

    // Deliberately implements the JSON Schema keywords used by the six embedded 2020-12
    // contracts, including local $ref, oneOf and strict objects. Not a general schema engine.
    private static void ValidateNode(JsonElement root, JsonElement schema, JsonElement value, string path)
    {
        void Fail(string message) => throw new ContractException($"{path}: {message}");
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var pointer = reference.GetString()!;
            if (!pointer.StartsWith("#/$defs/", StringComparison.Ordinal)) Fail("Unsupported schema reference.");
            ValidateNode(root, root.GetProperty("$defs").GetProperty(pointer[8..]), value, path);
            return;
        }
        if (schema.TryGetProperty("oneOf", out var variants))
        {
            var successes = 0;
            foreach (var variant in variants.EnumerateArray())
                try { ValidateNode(root, variant, value, path); successes++; } catch (ContractException) { }
            if (successes != 1) Fail("Expected exactly one allowed shape.");
            return;
        }
        if (schema.TryGetProperty("type", out var type))
        {
            bool Matches(string? name) => name switch
            {
                "object" => value.ValueKind == JsonValueKind.Object,
                "array" => value.ValueKind == JsonValueKind.Array,
                "string" => value.ValueKind == JsonValueKind.String,
                "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "null" => value.ValueKind == JsonValueKind.Null,
                "number" => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d) && double.IsFinite(d),
                "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n) && n == decimal.Truncate(n),
                _ => false
            };
            if (!(type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().Any(t => Matches(t.GetString())) : Matches(type.GetString())))
                Fail("Wrong JSON type or non-finite number.");
        }
        if (schema.TryGetProperty("const", out var constant) && !SameScalar(constant, value)) Fail("Unexpected constant/version.");
        if (schema.TryGetProperty("enum", out var allowed) && !allowed.EnumerateArray().Any(x => SameScalar(x, value))) Fail("Unknown enum.");
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out var required))
                foreach (var field in required.EnumerateArray())
                    if (!value.TryGetProperty(field.GetString()!, out _)) Fail($"Missing {field.GetString()}.");
            if (schema.TryGetProperty("properties", out var properties))
                foreach (var property in value.EnumerateObject())
                    if (properties.TryGetProperty(property.Name, out var child)) ValidateNode(root, child, property.Value, path + "." + property.Name);
                    else if (schema.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.False) Fail($"Unknown field {property.Name}.");
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            var count = value.GetArrayLength();
            if (schema.TryGetProperty("maxItems", out var max) && count > max.GetInt32() ||
                schema.TryGetProperty("minItems", out var min) && count < min.GetInt32()) Fail("Array size out of bounds.");
            if (schema.TryGetProperty("items", out var items))
                foreach (var child in value.EnumerateArray()) ValidateNode(root, items, child, path + "[]");
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!;
            if (schema.TryGetProperty("maxLength", out var max) && text.Length > max.GetInt32() ||
                schema.TryGetProperty("minLength", out var min) && text.Length < min.GetInt32()) Fail("String length out of bounds.");
            if (schema.TryGetProperty("pattern", out var pattern) && !Regex.IsMatch(text, pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) Fail("Invalid string format.");
            if (schema.TryGetProperty("format", out var format))
            {
                if (format.GetString() == "uuid" && !Guid.TryParseExact(text, "D", out _)) Fail("Invalid UUID.");
                if (format.GetString() == "date-time" && !DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out _)) Fail("Invalid timestamp.");
            }
        }
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetDouble(out var number) || !double.IsFinite(number)) Fail("Non-finite number.");
            if (schema.TryGetProperty("minimum", out var min) && number < min.GetDouble() ||
                schema.TryGetProperty("maximum", out var max) && number > max.GetDouble()) Fail("Number outside allowed range.");
        }
    }
    private static bool SameScalar(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind) return false;
        return left.ValueKind switch
        {
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.Number => left.TryGetDecimal(out var a) && right.TryGetDecimal(out var b) && a == b,
            JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False => true,
            _ => throw new ContractException("Contract enums/constants must be scalar.")
        };
    }
}
