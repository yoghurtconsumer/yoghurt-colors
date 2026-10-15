using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chromatic.Core.Models;
using Chromatic.Core.Validation;
using Chromatic.Infrastructure.Ipc;
using Chromatic.Infrastructure.Persistence;
using Xunit;
namespace Chromatic.Integration.Tests;
public sealed class SchemaAndMigrationTests
{
    [Fact] public void PresetSchemaRequiresPayloadMatchingKind()
    {
        Assert.Throws<ContractException>(() => Validate("preset", new PresetDocument { Kind = PresetKind.Resolution }));
        Assert.Throws<ContractException>(() => Validate("settings", new AppSettings { Crosshair = new(Shape: CrosshairShape.Picture) }));
    }
    private static void Validate(string schema, object value) => JsonContract.Validate(schema, JsonSerializer.SerializeToElement(value, value.GetType(), JsonContract.Options));
    private static JsonObject SettingsNode() => JsonSerializer.SerializeToNode(new AppSettings(), JsonContract.Options)!.AsObject();
    [Fact] public void CurrentDocumentsConformToEveryPublishedSchema()
    {
        Validate("settings", new AppSettings()); Validate("profile", new DisplayProfile()); Validate("preset", new PresetDocument());
        Validate("remote", new RemoteCommand(1, 1, 0, RemoteAction.GetState));
        Validate("recovery", RecoveryTests.ValidSnapshot());
        Validate("ipc", new PipeHello(1, "hello", 10, 100, 1, "S-1-5-21-1", new string('a', 64)));
    }
    [Theory] [InlineData("unknown")] [InlineData("null")] [InlineData("missing")] [InlineData("range")] [InlineData("enum")] [InlineData("version")]
    public void SchemaRejectsMalformedSettings(string kind)
    {
        var node = SettingsNode();
        switch (kind)
        {
            case "unknown": node["runShell"] = "calc.exe"; break;
            case "null": node["globalColor"] = null; break;
            case "missing": node.Remove("services"); break;
            case "range": node["globalColor"]!["saturationPercent"] = 301; break;
            case "enum": node["closeBehavior"] = "KeepUnrestored"; break;
            case "version": node["schemaVersion"] = 2; break;
        }
        using var doc = JsonContract.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()));
        Assert.Throws<ContractException>(() => JsonContract.Validate("settings", doc.RootElement));
    }
    [Fact] public void DuplicateFieldsAndDeeplyNestedDataAreRejected()
    {
        Assert.Throws<ContractException>(() => JsonContract.Parse(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"schemaVersion\":1}")));
        Assert.ThrowsAny<JsonException>(() => JsonContract.Parse(Encoding.UTF8.GetBytes(new string('[', 40) + "0" + new string(']', 40))));
    }
    [Fact] public void RemoteSchemaDoesNotAcceptDisplayCommandsOrUnknownKeys()
    {
        var node = JsonSerializer.SerializeToNode(new RemoteCommand(1, 1, 0, RemoteAction.SetColor, new()), JsonContract.Options)!;
        node["hdr"] = true;
        using var doc = JsonContract.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()), 4096);
        Assert.Throws<ContractException>(() => JsonContract.Validate("remote", doc.RootElement));
    }
    [Fact] public void MigrationPreservesIntentAndAddsDisabledServiceDefaults()
    {
        var source = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 0, effectsEnabled = true, color = new ColorSettings(175), closeToTray = true }, JsonContract.Options);
        var migrated = MigrationRunner.ToCurrent("settings", source);
        using var document = JsonContract.Parse(migrated); JsonContract.Validate("settings", document.RootElement);
        var settings = JsonSerializer.Deserialize<AppSettings>(migrated, JsonContract.Options)!;
        Assert.True(settings.EffectsEnabled); Assert.Equal(175, settings.GlobalColor.SaturationPercent);
        Assert.Equal(CloseBehavior.MinimizeToTray, settings.CloseBehavior); Assert.Equal(new OptionalServices(), settings.Services);
        Assert.Equal(migrated, MigrationRunner.ToCurrent("settings", migrated));
    }
    [Theory] [InlineData("{\"schemaVersion\":0,\"effectsEnabled\":false,\"color\":{},\"closeToTray\":false}")]
    [InlineData("{\"schemaVersion\":0,\"effectsEnabled\":false,\"color\":null,\"closeToTray\":false}")]
    [InlineData("{\"schemaVersion\":99}")]
    public void InvalidOrFutureLegacyDocumentsAreNotGuessed(string source) =>
        Assert.ThrowsAny<Exception>(() => MigrationRunner.ToCurrent("settings", Encoding.UTF8.GetBytes(source)));
    [Fact] public async Task MigratingLoadDoesNotRewriteTheOriginalFile()
    {
        using var folder = new TestDirectory();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 0, effectsEnabled = false, color = new ColorSettings(120), closeToTray = false }, JsonContract.Options);
        await File.WriteAllBytesAsync(folder.File(), bytes);
        var loaded = await new SettingsRepository(folder.File()).LoadAsync();
        Assert.Equal(1, loaded.Value.SchemaVersion); Assert.Equal(bytes, await File.ReadAllBytesAsync(folder.File()));
    }
}
