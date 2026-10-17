using Chromatic.Agent;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Xunit;

namespace Chromatic.Integration.Tests;

public sealed class AutomationRuntimeTests
{
    [Fact] public async Task ReleaseIsReducedBeforeSlowBoostCompletionWithoutBlockingInputConsumer()
    {
        using var fixture = new Fixture(); await fixture.MatchAsync();
        fixture.Client.HoldGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Key(0x75, true); await fixture.Runtime.TickAsync().WaitAsync(TimeSpan.FromSeconds(1)); Assert.True(fixture.Client.State.Held);
        fixture.Key(0x75, false); await fixture.Runtime.TickAsync().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(fixture.Client.State.Held); Assert.False(fixture.Client.HoldGate.Task.IsCompleted);
        fixture.Client.HoldGate.SetResult(); await fixture.Runtime.TickAsync();
    }
    [Fact] public async Task DefaultOffNeverConstructsNativeListeners()
    {
        var client = new Client(new());
        var calls = 0;
        await using var runtime = new AutomationRuntime(client, 999, _ => { }, _ => { calls++; throw new Exception("Must never construct."); });
        await runtime.TickAsync(); Assert.Empty(client.Commands); Assert.Equal(0, calls);
    }
    [Fact] public async Task RapidForegroundTransitionsDebounceAndUnknownDeniedOwnAndReusedPidReturnDesktop()
    {
        using var fixture = new Fixture(); await fixture.Runtime.TickAsync();
        fixture.Input.Foreground = new(11, new(@"C:\Games\other.exe", 10, 1));
        fixture.Input.Events.Enqueue(new(AutomationSignalKind.Foreground, Window: 11));
        fixture.Now = 100; await fixture.Runtime.TickAsync();
        fixture.Input.Foreground = Game(); fixture.Input.Events.Enqueue(new(AutomationSignalKind.Foreground, Window: 12));
        fixture.Now = 200; await fixture.Runtime.TickAsync(); fixture.Now = 349; await fixture.Runtime.TickAsync();
        Assert.Empty(fixture.Client.Commands);
        fixture.Now = 350; await fixture.Runtime.TickAsync(); Assert.Equal("game", fixture.Client.State.BaseProfile.Id);
        fixture.Input.Foreground = Game() with { Identity = Game().Identity! with { ProcessStartTicks = 2 } };
        fixture.Now = 600; await fixture.Runtime.TickAsync(); fixture.Now = 750; await fixture.Runtime.TickAsync();
        Assert.Equal(2, fixture.Client.State.Foreground!.ProcessStartTicks);
        foreach (var denied in new[] { new ForegroundObservation(12, null), new ForegroundObservation(15, new(@"C:\Games\game.exe", 999, 1)), new ForegroundObservation(0, null) })
        {
            fixture.Input.Foreground = denied; fixture.Input.Events.Enqueue(new(AutomationSignalKind.Foreground, Window: denied.Window));
            fixture.Now += 200; await fixture.Runtime.TickAsync(); fixture.Now += 150; await fixture.Runtime.TickAsync();
            Assert.Null(fixture.Client.State.GameProfile); Assert.Equal("desktop", fixture.Client.State.BaseProfile.Id); Assert.Null(fixture.Client.State.Foreground);
        }
    }
    [Fact] public async Task HeldProfileChangeMissedUpAndFocusLossRestoreNewestBase()
    {
        using var fixture = new Fixture(); await fixture.MatchAsync();
        fixture.Key(0x75, true); await fixture.Runtime.TickAsync(); Assert.True(fixture.Client.State.Held);
        fixture.Key(0x75, true); await fixture.Runtime.TickAsync(); Assert.Single(fixture.Client.Commands.OfType<SetHold>(), h => h.Held);
        await fixture.Client.SendAsync(fixture.Envelope(new ActivatePreset(new() { Id = "latest", GlobalColor = new(220) })));
        fixture.Key(0x75, false); await fixture.Runtime.TickAsync(); Assert.False(fixture.Client.State.Held); Assert.Equal(220, fixture.Client.State.BaseProfile.GlobalColor.SaturationPercent);
        fixture.Key(0x75, true); await fixture.Runtime.TickAsync(); Assert.True(fixture.Client.State.Held);
        fixture.Input.Down.Clear(); fixture.Now += 100; await fixture.Runtime.TickAsync(); Assert.False(fixture.Client.State.Held);
        fixture.Key(0x75, false); fixture.Key(0x75, true); await fixture.Runtime.TickAsync(); Assert.True(fixture.Client.State.Held);
        fixture.Input.Events.Enqueue(new(AutomationSignalKind.Foreground, Window: 20)); await fixture.Runtime.TickAsync(); Assert.False(fixture.Client.State.Held);
    }
    [Fact] public async Task SuspendAndOverflowClearHoldWithoutSyntheticPressOnResume()
    {
        using var fixture = new Fixture(); await fixture.MatchAsync(); fixture.Key(0x75, true); await fixture.Runtime.TickAsync();
        await fixture.Client.SendAsync(fixture.Envelope(new Suspend(SuspensionReason.Pause))); await fixture.Runtime.TickAsync();
        await fixture.Client.SendAsync(fixture.Envelope(new Resume(SuspensionReason.Pause))); fixture.Key(0x75, true); await fixture.Runtime.TickAsync(); Assert.False(fixture.Client.State.Held);
        fixture.Key(0x75, false); fixture.Key(0x75, true); await fixture.Runtime.TickAsync(); Assert.True(fixture.Client.State.Held);
        fixture.Input.Overflow = true; await fixture.Runtime.TickAsync(); Assert.False(fixture.Client.State.Held); Assert.True(fixture.Input.Disposed); Assert.Contains("overflow", fixture.Status);
    }
    [Fact] public async Task HotkeysDispatchRestorePausePresetAndUnavailableScreenshotAndExposeConflicts()
    {
        using var fixture = new Fixture(); fixture.Client.State = fixture.Client.State with { Automation = Config() with { HotkeysEnabled = true,
            Hotkeys = [new(1, 2, 118, HotkeyAction.Preset, "game"), new(2, 2, 119, HotkeyAction.Pause), new(3, 2, 120, HotkeyAction.Screenshot), new(4, 2, 121, HotkeyAction.Restore)] } };
        fixture.Input.Status = "Hotkey 5 conflict/error 1409"; await fixture.Runtime.TickAsync();
        foreach (var id in new[] { 1, 2, 2, 3, 4 }) { fixture.Input.Events.Enqueue(new(AutomationSignalKind.Hotkey, HotkeyId: id)); await fixture.Runtime.TickAsync(); }
        Assert.Contains(fixture.Client.Commands, c => c is ActivatePreset); Assert.Contains(fixture.Client.Commands, c => c is Resume);
        Assert.Contains(fixture.Client.Commands, c => c is TakeScreenshot); Assert.False(fixture.Client.State.Enabled); Assert.Contains("1409", fixture.Status);
    }
    [Theory] [InlineData(1.2, 0)] [InlineData(1, .1)]
    public void NonlinearHoldIsUnavailableUntilQualified(double gamma, double shadows)
        => Assert.False(AutomationRuntime.BasicLiftAvailable(new() { DesktopProfile = new() { GlobalColor = new(Gamma: gamma, Shadows: shadows) } }));
    private static ForegroundObservation Game() => new(12, new(@"C:\Games\game.exe", 10, 1));
    private static AutomationSettings Config() => new() { AutomaticProfiles = true, Boost = new(true), Profiles = [new() { Id = "game", GlobalColor = new(200) }], Rules = [new("g", @"C:\Games\game.exe", "game", true)] };
    private sealed class Input : IAutomationInput
    {
        public Queue<AutomationSignal> Events { get; } = new(); public HashSet<int> Down { get; } = [];
        public ForegroundObservation Foreground { get; set; } = Game(); public string Status { get; set; } = "Fake listeners";
        public bool Overflow; public bool Disposed;
        public bool Overflowed { get { var value = Overflow; Overflow = false; return value; } }
        public bool TryRead(out AutomationSignal signal) => Events.TryDequeue(out signal);
        public bool IsDown(int key) => Down.Contains(key);
        public ForegroundObservation ObserveForeground() => Foreground;
        public void Dispose() => Disposed = true;
    }
    private sealed class Client(EffectsState state) : IEffectsClient
    {
        public EffectsState State { get; set; } = state; public List<EffectsCommand> Commands { get; } = [];
        public TaskCompletionSource? HoldGate;
        public async Task<EffectsAcknowledgment> SendAsync(CommandEnvelope command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.Command); var reduced = EffectsReducer.Reduce(State, command); State = reduced.State;
            if (command.Command is SetHold { Held: true } && HoldGate is not null) await HoldGate.Task;
            var result = new ApplyResult(reduced.Result.Status == CompletionStatus.Accepted, "Fake", null, "Injected display acknowledgment", reduced.State.Revision);
            State = EffectsReducer.Observe(State, result, reduced.State.Generation); return new EffectsAcknowledgment(reduced.Result, State, result);
        }
    }
    private sealed class Fixture : IDisposable
    {
        public long Now; public Input Input { get; } = new(); public Client Client { get; } = new(new() { Enabled = true, Automation = Config(), HoldBoost = new(true) });
        public AutomationRuntime Runtime { get; } public string Status = "";
        public Fixture() { Runtime = new(Client, 999, text => Status = text, _ => Input, () => Now); }
        public CommandEnvelope Envelope(EffectsCommand command) => new(Guid.NewGuid(), Client.State.Revision, Client.State.Generation, command);
        public void Key(int key, bool down) { if (down) Input.Down.Add(key); else Input.Down.Remove(key); Input.Events.Enqueue(new(AutomationSignalKind.Key, new(key, down))); }
        public async Task MatchAsync() { await Runtime.TickAsync(); Now = 150; await Runtime.TickAsync(); }
        public void Dispose() => Runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
