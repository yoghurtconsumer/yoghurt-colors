using System.Collections.Immutable;
using Chromatic.App.ViewModels;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Xunit;

namespace Chromatic.Ui.Tests;
public sealed class ResolutionWorkflowTests
{
    private sealed class Client : IEffectsClient
    {
        public EffectsState State { get; private set; } = new() { Enabled = true, Revision = 7, Generation = 8 };
        public List<EffectsCommand> Commands { get; } = [];
        public bool Fail;
        public DisplayInventorySnapshot Snapshot { get; }
        public Client()
        {
            var outputs = Enumerable.Range(0, 5).Select(i => new DisplayOutputInfo(new("monitor-" + i, null, @"\\.\DISPLAY" + i, 1, (uint)i),
                "Monitor " + i, (uint)i, 1, i * 1920 - 1920, 0, new(1920, 1080, new(60000, 1001)), 1, 1, false, false, "SDR", "Fake")).ToImmutableArray();
            var modes = Enumerable.Range(0, 20).Select(i => new DisplayModeChoice("mode-" + i, new(1280, 960, new((uint)(60 + i), 1)), i == 0, "Fake driver")).ToImmutableArray();
            Snapshot = new(Guid.NewGuid(), outputs, outputs.ToImmutableDictionary(o => o.Display.MonitorDevicePath, _ => modes), "Injected read-only");
        }
        public void EnablePreview() => State = State with { Capabilities = new([new(BackendKind.DisplayMode, Capability.Experimental, "Fake preview", EffectScope.GlobalDesktop)], false) };
        public Task<EffectsAcknowledgment> SendAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default)
        {
            Commands.Add(envelope.Command);
            if (envelope.Command is not QueryDisplays)
            {
                var reduced = EffectsReducer.Reduce(State, envelope);
                if (!Fail)
                {
                    State = reduced.State;
                    if (envelope.Command is PreviewMode) State = State with { Preview = State.Preview! with { Status = PreviewStatus.AwaitingConfirmation } };
                    if (envelope.Command is CancelPreview) State = State with { Preview = State.Preview! with { Status = PreviewStatus.Reverted } };
                }
                return Task.FromResult(new EffectsAcknowledgment(reduced.Result, State, new(!Fail, "Fake", Fail ? "Failed" : null, Fail ? "Injected failure" : "Acknowledged", State.Revision)));
            }
            var query = (QueryDisplays)envelope.Command;
            var reduction = EffectsReducer.Reduce(State, envelope);
            return Task.FromResult(new EffectsAcknowledgment(reduction.Result, State, new(!Fail, "Fake", Fail ? "Failed" : null, Fail ? "Injected failure" : "Read", 7),
                Fail ? null : Snapshot.Page(query.Display, query.Offset)));
        }
    }
    [Fact] public async Task RefreshPagesMonitorsAndModesAndReviewNeverSendsAMutation()
    {
        var client = new Client(); var vm = new ResolutionViewModel(client);
        await vm.RefreshAsync(); Assert.Equal(5, vm.Outputs.Length); Assert.Empty(vm.Modes); Assert.Equal(2, client.Commands.Count);
        await vm.LoadModesAsync(); Assert.Equal(20, vm.Modes.Length); Assert.Equal(4, client.Commands.Count);
        vm.Scaling = ScalingIntent.Stretched; vm.ReviewPlanCommand.Execute(null);
        Assert.Contains("4 other active target", vm.Plan); Assert.Contains("Draft only", vm.Plan); Assert.Contains("game FOV", vm.Plan);
        Assert.All(client.Commands, command => Assert.IsType<QueryDisplays>(command)); Assert.True(client.State.Enabled); Assert.Equal(7, client.State.Revision);
        vm.SelectedOutput = vm.Outputs[1]; Assert.Empty(vm.Modes); Assert.Null(vm.SelectedMode);
        vm.ReviewPlanCommand.Execute(null); Assert.StartsWith("Plan unavailable", vm.Plan);
    }
    [Fact] public async Task PreviewKeepAndRevertUseTypedCommandsAndAuthoritativeAcknowledgments()
    {
        var client = new Client(); var vm = new ResolutionViewModel(client); await vm.RefreshAsync(); await vm.LoadModesAsync();
        Assert.False(vm.CanPreview); await Assert.ThrowsAsync<InvalidOperationException>(vm.PreviewAsync);
        client.EnablePreview(); await vm.PreviewAsync(); Assert.True(vm.IsPreviewActive); Assert.True(vm.CanKeep);
        var preview = Assert.IsType<PreviewMode>(client.Commands.Last()); Assert.Equal(vm.SelectedOutput!.Display, preview.Display);
        client.Fail = true; await vm.KeepAsync(); Assert.True(vm.IsPreviewActive); Assert.Contains("Injected failure", vm.Status);
        client.Fail = false; await vm.KeepAsync(); Assert.False(vm.IsPreviewActive); Assert.IsType<ConfirmMode>(client.Commands.Last());
        await vm.PreviewAsync(); await vm.RevertAsync(); Assert.IsType<CancelPreview>(client.Commands.Last()); Assert.False(vm.IsPreviewActive);
    }
    [Fact] public async Task FailedRefreshClearsStaleSelectionAndDisconnectedUiDoesNotPretendSuccess()
    {
        var client = new Client(); var vm = new ResolutionViewModel(client); await vm.RefreshAsync(); await vm.LoadModesAsync();
        client.Fail = true; await Assert.ThrowsAsync<InvalidOperationException>(vm.RefreshAsync);
        Assert.Empty(vm.Outputs); Assert.Empty(vm.Modes); Assert.Null(vm.SelectedOutput); Assert.Null(vm.SelectedMode);
        await Assert.ThrowsAsync<InvalidOperationException>(new ResolutionViewModel().RefreshAsync);
    }
}
