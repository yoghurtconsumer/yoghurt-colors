using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
namespace Chromatic.Agent;

// In-process foundation for simple injected tests. Production uses the
// JournaledEffectsCoordinator in a separate Agent, never an App-hosted writer.
public sealed class EffectsCoordinator(IColorBackend backend, EffectsState? initial = null) : IEffectsClient, IDisposable
{
    private readonly SemaphoreSlim _queue = new(1, 1);
    public EffectsState State { get; private set; } = initial ?? new();
    public async Task<EffectsAcknowledgment> SendAsync(CommandEnvelope command, CancellationToken cancellationToken = default)
    {
        await _queue.WaitAsync(cancellationToken);
        try
        {
            var reduction = EffectsReducer.Reduce(State, command);
            State = reduction.State;
            if (reduction.Result.Status != CompletionStatus.Accepted)
                return new(reduction.Result, State, new(false, "Coordinator", reduction.Result.Status.ToString(), reduction.Result.Explanation, State.Revision));
            ApplyResult result;
            if (command.Command is PreviewMode or ConfirmMode or CancelPreview)
                result = ApplyResult.NotImplemented("DisplayTransaction", State.Revision);
            else
            {
                try
                {
                    result = State.Effective.Enabled
                        ? await backend.ApplyAsync(State.Effective.GlobalColor, State.Revision, cancellationToken)
                        : await backend.RestoreAsync(cancellationToken);
                    result = result with { Revision = State.Revision };
                }
                catch (Exception e) when (e is not OperationCanceledException)
                { result = new(false, "Coordinator", "BackendFailure", e.GetType().Name, State.Revision); }
            }
            State = EffectsReducer.Observe(State, result, State.Generation);
            return new(reduction.Result, State, result);
        }
        finally { _queue.Release(); }
    }
    public void Dispose() => _queue.Dispose();
}
