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
        var foreign = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => AppInstance.StartOrForwardAsync(Path.Combine(Path.GetDirectoryName(image)!, "wrong.exe"), null, _ => { }, scope));
        Assert.Contains("Exit the older copy", foreign.Message);
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
    [Fact] public async Task SynchronousUiShutdownDuringHandoffReleasesPipeAndAllowsRestart()
    {
        var image = Environment.ProcessPath!; var scope = Guid.NewGuid().ToString("N");
        using var received = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        using var stopping = new ManualResetEventSlim();
        var context = new QueuedUiContext(); var completed = false; Exception? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                var primary = AppInstance.StartOrForwardAsync(image, null, _ =>
                { received.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new InvalidOperationException("Test handoff not released."); }, scope).GetAwaiter().GetResult()!;
                stopping.Set();
                Assert.True(received.Wait(TimeSpan.FromSeconds(10)));
                var close = primary.DisposeAsync().AsTask();
                release.Set();
                // Application.OnExit blocks its UI thread here; continuations
                // posted to this context cannot run until after shutdown.
                completed = close.Wait(TimeSpan.FromSeconds(3));
                context.Drain(); close.GetAwaiter().GetResult();
            }
            catch (Exception error) { failure = error; release.Set(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(stopping.Wait(TimeSpan.FromSeconds(10)));
        var forwarding = Task.Run(async () =>
        {
            try { await AppInstance.StartOrForwardAsync(image, null, _ => { }, scope); }
            catch (Exception error) when (error is IOException or OperationCanceledException or TimeoutException) { }
        });
        Assert.True(await Task.Run(() => thread.Join(TimeSpan.FromSeconds(15))), "UI shutdown hung.");
        await forwarding;
        Assert.Null(failure); Assert.True(completed, "Shutdown captured the blocked UI context and retained the pipe.");
        await using var restarted = await AppInstance.StartOrForwardAsync(image, null, _ => { }, scope);
        Assert.NotNull(restarted);
    }
    [Fact] public async Task UnresponsiveExistingInstanceReportsActionableErrorWithoutStartingSecondServer()
    {
        var scope = Guid.NewGuid().ToString("N"); var name = AppInstance.PipeName(PipeInterop.Current, scope);
        using var server = PipeInterop.CreateCurrentUserLocalServer(name);
        var accept = server.WaitForConnectionAsync();
        var error = await Assert.ThrowsAsync<TimeoutException>(() => AppInstance.StartOrForwardAsync(Environment.ProcessPath!, null, _ => { }, scope));
        await accept;
        Assert.Contains("Exit the older copy", error.Message); Assert.Contains("Task Manager", error.Message);
        Assert.IsAssignableFrom<OperationCanceledException>(error.InnerException);
        Assert.Throws<System.ComponentModel.Win32Exception>(() => PipeInterop.CreateCurrentUserLocalServer(name));
    }
    [Fact] public async Task DelayedAcknowledgmentAndRapidSequentialLaunchesKeepPipeUsable()
    {
        var scope = Guid.NewGuid().ToString("N"); var received = 0;
        await using var primary = await AppInstance.StartOrForwardAsync(Environment.ProcessPath!, null, _ => Interlocked.Increment(ref received), scope);
        Assert.NotNull(primary);
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6)))
        using (var client = new NamedPipeClientStream(".", AppInstance.PipeName(PipeInterop.Current, scope), PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await client.ConnectAsync(timeout.Token);
            using var peer = AppInstance.Authenticate(client, false, Environment.ProcessPath!);
            var challenge = JsonSerializer.Deserialize<AppHandoff>(await Protocol.ReadFrameAsync(client, timeout.Token), JsonContract.Options)!;
            await Protocol.WriteFrameAsync(client, JsonSerializer.SerializeToUtf8Bytes(new AppHandoff(1, "request", challenge.Challenge), JsonContract.Options), timeout.Token);
            while (Volatile.Read(ref received) == 0) await Task.Delay(1, timeout.Token);
            // A completed pipe write does not mean the client consumed it.
            // Disconnect must not discard an unread acknowledgment.
            await Task.Delay(100, timeout.Token);
            var ack = JsonSerializer.Deserialize<AppHandoff>(await Protocol.ReadFrameAsync(client, timeout.Token), JsonContract.Options)!;
            Assert.Equal("accepted", ack.Kind); Assert.Equal(challenge.Challenge, ack.Challenge);
            await Protocol.WriteFrameAsync(client, JsonSerializer.SerializeToUtf8Bytes(ack, JsonContract.Options), timeout.Token);
            Assert.Equal(0, await client.ReadAsync(new byte[1], timeout.Token));
        }
        for (var i = 0; i < 32; i++)
            Assert.Null(await AppInstance.StartOrForwardAsync(Environment.ProcessPath!, null, _ => throw new InvalidOperationException("Second server"), scope));
        Assert.Equal(33, Volatile.Read(ref received));
    }
    private sealed class QueuedUiContext : SynchronizationContext
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _work = new();
        public override void Post(SendOrPostCallback callback, object? state) => _work.Enqueue(() => callback(state));
        public void Drain() { while (_work.TryDequeue(out var action)) action(); }
    }
    private sealed class RegistryFake : IProtocolRegistry
    {
        public string? Value; public string? ReadCommand() => Value;
        public void Write(string command) => Value = command; public void Delete() => Value = null;
    }
    private sealed class Fault(WriteCheckpoint at) : IWriteFaultInjector
    { public void Check(WriteCheckpoint checkpoint) { if (checkpoint == at) throw new IOException("Injected failure."); } }
}
