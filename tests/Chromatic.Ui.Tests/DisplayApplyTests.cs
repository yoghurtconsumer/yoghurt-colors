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
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task DisplayUsesTypedCommandsAndShowsBackendFailureWithoutSavingHeldState(bool fail)
    {
        var client = new FakeClient(fail); var session = new AppSessionSettings(new());
        var vm = new DisplayViewModel(client, session) { Saturation = 180, VibranceScale = 75, CvdMode = "GreenWeak", CvdStrength = .4 };
        Assert.True(vm.CanApply); Assert.Equal(.5, vm.Vibrance);
        await vm.ApplyAsync();
        Assert.IsType<Enable>(client.Commands[0]); Assert.IsType<ActivatePreset>(client.Commands[1]); Assert.IsType<Enable>(client.Commands[^1]);
        Assert.Contains(fail ? "InjectedFailure" : "FakeCompleted", vm.ApplyStatus);
        Assert.Equal(fail ? 100 : 180, session.Current.GlobalColor.SaturationPercent);
        Assert.False(session.Current.EffectsEnabled); Assert.False(session.Current.HoldBoost.Enabled);
        Assert.Equal(.4, client.State.BaseProfile.GlobalColor.CvdStrength);
    }
    private sealed class FakeClient(bool fail) : IEffectsClient
    {
        public List<EffectsCommand> Commands { get; } = [];
        public EffectsState State { get; private set; } = new()
        {
            Capabilities = new(ImmutableArray.Create(new BackendCapability(BackendKind.Magnification, Capability.Available,
                "Injected fake capability; no hardware.", EffectScope.GlobalDesktop)))
        };
        public Task<EffectsAcknowledgment> SendAsync(CommandEnvelope command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.Command); var reduction = EffectsReducer.Reduce(State, command); State = reduction.State;
            var failed = fail && command.Command is Enable { Enabled: true };
            return Task.FromResult(new EffectsAcknowledgment(reduction.Result, State,
                new(!failed, "InjectedFake", failed ? "InjectedFailure" : "FakeCompleted", "Test fixture only.", State.Revision)));
        }
    }
}
