using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Persistence;
using System.IO.Pipes;
using System.Collections.Concurrent;
namespace Chromatic.Infrastructure.Ipc;
public sealed class EffectsPipeClient : IEffectsClient, IDisposable
{
    private PipeStream? _pipe;
    private readonly SemaphoreSlim _writer = new(1, 1);
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<IpcResponse>> _pending = new();
    private readonly CancellationTokenSource _lifetime = new();
    private EffectsState _state = new();
    public EffectsState State => Volatile.Read(ref _state);
    public bool Connected => _pipe?.IsConnected == true;
    public EffectsPipeClient() { }
    public EffectsPipeClient(PipeStream authenticatedPipe) { _pipe = authenticatedPipe; _ = ReadResponsesAsync(authenticatedPipe); }
    public async Task<EffectsAcknowledgment> SendAsync(CommandEnvelope command, CancellationToken cancellationToken = default)
    {
        if (_pipe is null) return new(new(CompletionStatus.Blocked, State.Revision, State.Generation,
            "Authenticated Agent transport is disconnected."), State, new(false, "EffectsPipe", "Disconnected", "Authenticated Agent transport is disconnected.", State.Revision));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var waiting = new TaskCompletionSource<IpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_pending.Count >= 32 || !_pending.TryAdd(command.CommandId, waiting)) throw new ContractException("Too many outstanding or duplicate IPC commands.");
        try
        {
            var request = new IpcRequest(1, "command", Chromatic.Native.PipeInterop.Current.SessionId, command);
            await _writer.WaitAsync(timeout.Token);
            try { await Protocol.WriteFrameAsync(_pipe ?? throw new IOException("Agent pipe closed."), Protocol.Encode(request), timeout.Token); }
            finally { _writer.Release(); }
            var response = await waiting.Task.WaitAsync(timeout.Token);
            return new(response.Decision, State, response.Completion);
        }
        catch
        {
            // A timed out frame cannot be reused: late responses would break correlation.
            Disconnect(new IOException("Agent command ended without a complete correlated response.")); throw;
        }
        finally { _pending.TryRemove(command.CommandId, out _); }
    }
    public Task<EffectsAcknowledgment> QueryAsync(CancellationToken cancellationToken = default) =>
        SendAsync(new(Guid.NewGuid(), State.Revision, State.Generation, new GetState()), cancellationToken);
    private async Task ReadResponsesAsync(PipeStream pipe)
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                var response = Protocol.Decode<IpcResponse>(await Protocol.ReadFrameAsync(pipe, _lifetime.Token));
                if (!_pending.TryGetValue(response.CommandId, out var waiting)) throw new ContractException("IPC correlation mismatch.");
                var next = response.State.ToEffectsState(); var current = State;
                if (next.Revision > current.Revision || next.Revision == current.Revision && next.Generation >= current.Generation)
                {
                    if (next.Observed.LastResult is null && next.Revision == current.Revision) next = next with { Observed = current.Observed };
                    Volatile.Write(ref _state, next);
                }
                if (!waiting.TrySetResult(response)) throw new ContractException("Duplicate IPC response.");
            }
        }
        catch (Exception error) { Disconnect(error); }
    }
    private void Disconnect(Exception error)
    {
        _lifetime.Cancel(); Interlocked.Exchange(ref _pipe, null)?.Dispose();
        foreach (var waiting in _pending.Values) waiting.TrySetException(error);
    }
    public void Dispose() => Disconnect(new IOException("Agent client disposed."));
}
