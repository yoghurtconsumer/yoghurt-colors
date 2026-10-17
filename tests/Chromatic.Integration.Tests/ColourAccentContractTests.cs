using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Core.Validation;
using Chromatic.Infrastructure.Ipc;
using Chromatic.Infrastructure.Persistence;
using Xunit;

namespace Chromatic.Integration.Tests;

public sealed class ColourAccentContractTests
{
    private static readonly ColorSettings Accent = new(150) { HueRotationDegrees = 15, DarkenHueDegrees = 255, DarkenStrength = 4 };
    [Fact] public async Task ExtendedLocalProfilesRoundTripSettingsLibraryShareAndTypedIpc()
    {
        using var directory = new TestDirectory(); var settings = new SettingsRepository(directory.File());
        await settings.SaveAsync(new() { GlobalColor = Accent }); Assert.Equal(Accent, (await settings.LoadAsync()).Value.GlobalColor);
        var preset = new PresetDocument { Color = Accent, Name = "Blue-accent test" };
        var imported = PresetExchange.ImportCode(PresetExchange.Share(preset)); Assert.Equal(Accent, imported.Color);
        Assert.Contains("colour darkening 0 → 400%", PresetExchange.Diff(imported, new()));
        var library = new LibraryRepository(directory.File("library.json")); await library.SaveAsync(new() { Presets = [preset] });
        Assert.Equal(Accent, (await library.LoadAsync()).Value.Presets[0].Color);
        var profile = new DisplayProfile { GlobalColor = Accent };
        using var json = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(profile, JsonContract.Options)); JsonContract.Validate("profile", json.RootElement);
        var ipc = Protocol.Decode<IpcRequest>(Protocol.Encode(new IpcRequest(1, "command", 1, new(Guid.NewGuid(), 0, 0, new ApplyProfile(profile)))));
        Assert.Equal(Accent, Assert.IsType<ApplyProfile>(ipc.Envelope.Command).Profile.GlobalColor);
        var response = new IpcResponse(1, "result", Guid.NewGuid(), new(CompletionStatus.Accepted, 0, 0, "Fake"), new(true, "Fake", null, "Injected", 0),
            IpcState.From(new() { DesktopProfile = profile }));
        Assert.Equal(Accent, Protocol.Decode<IpcResponse>(Protocol.Encode(response)).State.GlobalColor);
    }
    [Fact] public void LegacyWireShapeStaysTenControlsAndLoadsWithTheNewControlsOff()
    {
        var json = JsonSerializer.SerializeToElement(new ColorSettings(), JsonContract.Options);
        Assert.Equal(10, json.EnumerateObject().Count());
        var preset = PresetExchange.Decode(PresetExchange.Encode(new())); Assert.Equal(0, preset.Color!.DarkenStrength); Assert.Equal(0, preset.Color.HueRotationDegrees);
    }
    [Theory] [InlineData("darkenStrength", 4.1)] [InlineData("darkenHueDegrees", 361)] [InlineData("hueRotationDegrees", -181)]
    public void SchemaRejectsExtendedOutOfRangeValues(string field, double value)
    {
        var node = JsonSerializer.SerializeToNode(new PresetDocument { Color = Accent }, JsonContract.Options)!; node["color"]![field] = value;
        using var json = JsonDocument.Parse(node.ToJsonString()); Assert.Throws<ContractException>(() => JsonContract.Validate("preset", json.RootElement));
    }
    [Fact] public void RemoteWhitelistIsNotExpandedByLocalColourExtension()
    {
        var remote = new RemoteCommand(1, 1, 0, RemoteAction.SetColor, Accent);
        Assert.Throws<ArgumentException>(() => RemoteCommandValidator.Validate(remote, 0, new HashSet<string>()));
        using var json = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(remote, JsonContract.Options));
        Assert.Throws<ContractException>(() => JsonContract.Validate("remote", json.RootElement));
        RemoteCommandValidator.Validate(remote with { Color = new() }, 0, new HashSet<string>());
    }
}
