using System.Collections.Immutable;
using Chromatic.App;
using Chromatic.App.ViewModels;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Xunit;

namespace Chromatic.Ui.Tests;

public sealed class DisplayApplyTests
{
    [Fact] public void NvidiaPreparationSelectsSingleOutputAndOnlyChangesTheDraft()
    {
        var client = new FakeClient(false); var display = new DisplayIdentity("fake-monitor", null, @"\\.\DISPLAY1", 1, 1);
        client.State = client.State with { Capabilities = new([new(BackendKind.NvidiaDvc, Capability.Experimental,
            "Injected opt-in", EffectScope.PerMonitor, display)]) };
        var vm = new DisplayViewModel(client) { Saturation = 220, Gamma = 1.4, OutputWarmth = .5 };
        vm.PrepareNvidiaCommand.Execute(null);
        Assert.Equal(100, vm.Saturation); Assert.Equal(75, vm.VibranceScale); Assert.Equal(1, vm.Gamma);
        Assert.Equal(0, vm.OutputWarmth); Assert.Equal(display, vm.SelectedOutput);
        Assert.Empty(client.Commands); Assert.Contains("Nothing applied", vm.ApplyStatus);
        Assert.Contains("Magnification+NvidiaDvc", vm.FullscreenStatus);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task DisplayUsesTypedCommandsAndShowsBackendFailureWithoutSavingHeldState(bool fail)
    {
        var client = new FakeClient(fail); var session = new AppSessionSettings(new());
        var vm = new DisplayViewModel(client, session) { Saturation = 180, VibranceScale = 75, CvdMode = "GreenWeak", CvdStrength = .4 };
        Assert.True(vm.CanApply); Assert.Equal(.5, vm.Vibrance);
        await vm.ApplyAsync();
        Assert.IsType<ApplyProfile>(Assert.Single(client.Commands));
        Assert.Contains(fail ? "InjectedFailure" : "Applied 180%", vm.ApplyStatus);
        Assert.Equal(fail ? 100 : 180, session.Current.GlobalColor.SaturationPercent);
        Assert.False(session.Current.EffectsEnabled); Assert.False(session.Current.HoldBoost.Enabled);
        Assert.Equal(.4, client.State.BaseProfile.GlobalColor.CvdStrength);
    }
    [Fact] public async Task BlockedApplyShowsHdrReasonWithoutSendingCommands()
    {
        var client = new FakeClient(false);
        client.State = client.State with { Capabilities = new([new(BackendKind.Magnification, Capability.Unsupported,
            "HDR/advanced color is active. Use SDR.", EffectScope.GlobalDesktop)], true) };
        var vm = new DisplayViewModel(client) { Saturation = 180 };
        await vm.ApplyAsync();
        Assert.Contains("HDR/advanced color", vm.ApplyStatus); Assert.Empty(client.Commands);
        var disconnected = new DisplayViewModel(); await disconnected.ApplyAsync();
        Assert.Contains("not connected", disconnected.ApplyStatus);
    }
    [Fact] public void OutputSelectionSurvivesRefreshAndTracksStableMonitorAfterMappingChange()
    {
        var client = new FakeClient(false); var display = new DisplayIdentity("monitor", "serial", @"\\.\DISPLAY1", 1, 1);
        void Outputs(params DisplayIdentity[] displays) => client.State = client.State with
        { Capabilities = new(displays.Select(d => new BackendCapability(BackendKind.SdrGamma, Capability.Available, "Fake", EffectScope.PerMonitor, d)).ToImmutableArray()) };
        Outputs(display); var vm = new DisplayViewModel(client); vm.SelectedOutput = display;
        var original = vm.Outputs; vm.Refresh();
        Assert.Equal(original, vm.Outputs); Assert.Equal(display, vm.SelectedOutput); Assert.True(vm.CanUseGamma);
        var remapped = display with { GdiName = @"\\.\DISPLAY2", TargetId = 2 };
        Outputs(remapped); vm.Refresh(); Assert.Equal(remapped, vm.SelectedOutput);
        Outputs(); vm.Refresh(); Assert.Null(vm.SelectedOutput); Assert.False(vm.CanUseGamma);
    }
    [Fact] public async Task UnsupportedTonesHaveAnExplicitResetPathPreservingSaturation()
    {
        var client = new FakeClient(false); var vm = new DisplayViewModel(client) { Saturation = 180, Gamma = 1.2 };
        await vm.ApplyAsync(); Assert.Contains("Cannot apply optional tones", vm.ApplyStatus); Assert.Empty(client.Commands);
        vm.ResetTonesCommand.Execute(null); Assert.Equal(180, vm.Saturation); Assert.Equal(1, vm.Gamma);
        await vm.ApplyAsync(); Assert.Contains("Applied 180%", vm.ApplyStatus);
    }
    [Fact] public async Task ApplyReportsProgressRejectsDuplicateAndShowsTransportFailure()
    {
        var client = new FakeClient(false) { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var session = new AppSessionSettings(new()); var vm = new DisplayViewModel(client, session) { Saturation = 180 };
        var applying = vm.ApplyAsync(); Assert.True(vm.IsApplying); Assert.Contains("waiting for Agent readback", vm.ApplyStatus);
        await vm.ApplyAsync(); Assert.Single(client.Commands);
        client.Pending.SetException(new System.IO.IOException("Disconnected fixture pipe")); await applying;
        Assert.False(vm.IsApplying); Assert.Contains("Not applied", vm.ApplyStatus); Assert.Contains("Disconnected fixture pipe", vm.ApplyStatus);
        Assert.Equal(100, session.Current.GlobalColor.SaturationPercent);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task SuccessfulTransportCannotClaimApplyWhenSuspendedOrSuperseded(bool superseded)
    {
        var client = new FakeClient(false) { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var vm = new DisplayViewModel(client) { Saturation = 180 }; var applying = vm.ApplyAsync();
        client.Pending.SetResult(new(new(CompletionStatus.Accepted, 1, 1, "Accepted"),
            new() { Enabled = superseded, Revision = superseded ? 2 : 1, Generation = superseded ? 2 : 1 },
            new(true, "Fake", null, "Suspended before completion.", 1)));
        await applying; Assert.StartsWith("Not applied", vm.ApplyStatus);
    }
    private sealed class FakeClient(bool fail) : IEffectsClient
    {
        public List<EffectsCommand> Commands { get; } = [];
        public TaskCompletionSource<EffectsAcknowledgment>? Pending { get; set; }
        public EffectsState State { get; set; } = new()
        {
            Capabilities = new(ImmutableArray.Create(new BackendCapability(BackendKind.Magnification, Capability.Available,
                "Injected fake capability; no hardware.", EffectScope.GlobalDesktop)))
        };
        public Task<EffectsAcknowledgment> SendAsync(CommandEnvelope command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.Command);
            if (Pending is not null) return Pending.Task;
            var reduction = EffectsReducer.Reduce(State, command); State = reduction.State;
            var failed = fail && command.Command is ApplyProfile;
            return Task.FromResult(new EffectsAcknowledgment(reduction.Result, State,
                new(!failed, "InjectedFake", failed ? "InjectedFailure" : "FakeCompleted", "Test fixture only.", State.Revision)));
        }
    }
}
