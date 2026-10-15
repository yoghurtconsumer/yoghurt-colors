using Chromatic.Core.Models;
using Chromatic.Core.State;
namespace Chromatic.Core.Abstractions;
public sealed record EffectsAcknowledgment(CommandResult Decision, EffectsState State, ApplyResult Completion);
public interface IEffectsClient
{
    EffectsState State { get; }
    Task<EffectsAcknowledgment> SendAsync(CommandEnvelope command, CancellationToken cancellationToken = default);
}
