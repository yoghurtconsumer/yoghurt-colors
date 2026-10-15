using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
namespace Chromatic.Infrastructure.Ipc;
public sealed class EffectsPipeClient : IEffectsClient
{
    public EffectsState State { get; } = new();
    public Task<EffectsAcknowledgment> SendAsync(CommandEnvelope command, CancellationToken cancellationToken = default) =>
        Task.FromResult(new EffectsAcknowledgment(new(CompletionStatus.NotImplemented, State.Revision, State.Generation,
            "Authenticated Agent transport is not connected in Stage 1."), State, ApplyResult.NotImplemented("EffectsPipe", State.Revision)));
}
