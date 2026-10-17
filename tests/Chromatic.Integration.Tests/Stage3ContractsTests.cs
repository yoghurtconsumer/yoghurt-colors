using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Ipc;
using Chromatic.Infrastructure.Persistence;
using Chromatic.Native;
using Xunit;

namespace Chromatic.Integration.Tests;

public sealed class Stage3ContractsTests
{
    [Theory] [InlineData(WriteCheckpoint.BeforeFlush, false)] [InlineData(WriteCheckpoint.BeforeReplace, false)] [InlineData(WriteCheckpoint.AfterReplace, true)]
    public async Task FailedAutomationWriteReportsErrorAndLeavesAuthoritativeCommittedDocument(WriteCheckpoint at, bool committed)
    {
        using var directory = new TestDirectory(); var path = directory.File(); var normal = new AutomationRepository(path); await normal.SaveAsync(new());
        await Assert.ThrowsAsync<IOException>(() => new AutomationRepository(path, new Fault(at)).SaveAsync(new() { HotkeysEnabled = true }));
        Assert.Equal(committed, (await normal.LoadAsync()).Value.HotkeysEnabled);
    }
    [Fact] public async Task AutomationRoundTripsSchemaRejectsFutureUnknownAndRecoversCorruptPrimary()
    {
        var root = Path.Combine(Path.GetTempPath(), "Chromatic.AutomationTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "automation.json"); var repository = new AutomationRepository(path);
            var initial = await repository.LoadAsync(); Assert.False(initial.Value.NeedsListener);
            await repository.SaveAsync(new()); await repository.SaveAsync(new() { HotkeysEnabled = true });
            await File.WriteAllTextAsync(path, "corrupt");
            var recovered = await repository.LoadAsync(); Assert.False(recovered.Value.HotkeysEnabled); Assert.NotNull(recovered.Warning);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new AutomationSettings(), JsonContract.Options);
            using var valid = JsonDocument.Parse(bytes); JsonContract.Validate("automation", valid.RootElement);
            foreach (var json in new[] { Encoding.UTF8.GetString(bytes).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"), Encoding.UTF8.GetString(bytes).Insert(1, "\"rawNativeCall\":1,") })
            { using var invalid = JsonDocument.Parse(json); Assert.Throws<ContractException>(() => JsonContract.Validate("automation", invalid.RootElement)); }
            var frame = new IpcRequest(1, "command", PipeInterop.Current.SessionId, new(Guid.NewGuid(), 0, 0, new ConfigureAutomation(new())));
            Assert.IsType<ConfigureAutomation>(Protocol.Decode<IpcRequest>(Protocol.Encode(frame)).Envelope.Command);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public void ProtocolRegistrationQuotesDataAndRefusesForeignOwnershipWithoutTouchingRegistry()
    {
        var registry = new RegistryFake(); var registration = new PresetProtocolRegistration(@"C:\My Apps\Chromatic.App.exe", registry);
        Assert.Null(registry.Value); Assert.Equal("\"" + @"C:\My Apps\Chromatic.App.exe" + "\" \"%1\"", registration.Command);
        registration.Register(); Assert.Contains("Registered", registration.Status); registration.Unregister(); Assert.Null(registry.Value);
        registry.Value = "foreign"; Assert.Throws<UnauthorizedAccessException>(registration.Register); Assert.Throws<UnauthorizedAccessException>(registration.Unregister); Assert.Equal("foreign", registry.Value);
        Assert.Throws<ArgumentException>(() => new PresetProtocolRegistration("C:\\bad\".exe", registry));
    }
    [Fact] public void ArgvIsStrictDataAndRejectsCommandsOrMixedSwitches()
    {
        Assert.Null(AppInstance.ParseArguments([]));
        var link = "chromaticdesk://preset/" + PresetExchange.Share(new()); Assert.Equal(link, AppInstance.ParseArguments([link]));
        foreach (var args in new[] { new[] { "--apply" }, new[] { link, "--apply" }, new[] { "chromaticdesk://exec/cmd.exe" }, new[] { link + "?apply=true" } })
            Assert.Throws<ContractException>(() => AppInstance.ParseArguments(args));
    }
    [Fact] public async Task SingleInstancePinsOsPeerRejectsForeignImageAndForwardsOnlyValidatedLinks()
    {
        var image = Environment.ProcessPath!; var received = new List<string?>(); var gate = new object(); var scope = Guid.NewGuid().ToString("N");
        await using var primary = await AppInstance.StartOrForwardAsync(image, null, value => { lock (gate) received.Add(value); }, scope); Assert.NotNull(primary);
        var link = "chromaticdesk://preset/" + PresetExchange.Share(new());
        Assert.Null(await AppInstance.StartOrForwardAsync(image, link, _ => throw new Exception("Must not start second server"), scope));
        Assert.Null(await AppInstance.StartOrForwardAsync(image, null, _ => { }, scope));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => AppInstance.StartOrForwardAsync(Path.Combine(Path.GetDirectoryName(image)!, "wrong.exe"), null, _ => { }, scope));
        lock (gate) { Assert.Equal(new string?[] { link, null }, received); }
        // A rejected client may leave the server finishing its bounded 3 s
        // handshake timeout; allow cleanup before the next adversarial request.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        using var pipe = new NamedPipeClientStream(".", AppInstance.PipeName(PipeInterop.Current, scope), PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(timeout.Token);
        Assert.Throws<UnauthorizedAccessException>(() => AppInstance.Authenticate(pipe, false, Path.Combine(Path.GetDirectoryName(image)!, "wrong.exe")));
        var challenge = await Protocol.ReadFrameAsync(pipe, timeout.Token);
        var text = Encoding.UTF8.GetString(challenge); Assert.Contains("challenge", text);
        await Protocol.WriteFrameAsync(pipe, Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"kind\":\"request\",\"challenge\":\"00000000000000000000000000000000\",\"link\":null}"), timeout.Token);
        await Assert.ThrowsAnyAsync<IOException>(async () => await Protocol.ReadFrameAsync(pipe, timeout.Token));
        lock (gate) Assert.Equal(2, received.Count);
    }
    private sealed class RegistryFake : IProtocolRegistry
    {
        public string? Value; public string? ReadCommand() => Value;
        public void Write(string command) => Value = command; public void Delete() => Value = null;
    }
    private sealed class Fault(WriteCheckpoint at) : IWriteFaultInjector
    { public void Check(WriteCheckpoint checkpoint) { if (checkpoint == at) throw new IOException("Injected failure."); } }
}
