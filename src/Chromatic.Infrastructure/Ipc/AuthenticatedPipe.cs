using System.IO.Pipes;
using Chromatic.Core.Abstractions;
using Chromatic.Core.State;
using Chromatic.Native;
using System.Collections.Concurrent;
using Chromatic.Infrastructure.Persistence;

namespace Chromatic.Infrastructure.Ipc;

public sealed record PipeLaunchIdentity(string PipeName, string Nonce, VerifiedPipePeer ExpectedPeer);
public static class AuthenticatedPipe
{
    public static PipeHello Hello(string nonce)
    {
        var identity = PipeInterop.Current;
        return new(1, "hello", identity.ProcessId, identity.StartTicks, identity.SessionId, identity.UserSid, nonce);
    }
    public static async Task AcceptAsync(NamedPipeServerStream stream, PipeLaunchIdentity launch, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await stream.WaitForConnectionAsync(timeout.Token);
        await AuthenticateAsync(stream, launch, serverSide: true, timeout.Token);
    }
    public static async Task<NamedPipeClientStream> ConnectAsync(PipeLaunchIdentity launch, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var stream = new NamedPipeClientStream(".", launch.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await stream.ConnectAsync(timeout.Token);
            await AuthenticateAsync(stream, launch, serverSide: false, timeout.Token);
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }
    private static async Task AuthenticateAsync(PipeStream stream, PipeLaunchIdentity launch, bool serverSide, CancellationToken cancellationToken)
    {
        var expected = launch.ExpectedPeer;
        var current = PipeInterop.Current;
        if (!expected.IsLocal || current.UserSid != expected.UserSid || current.SessionId != expected.SessionId)
            throw new UnauthorizedAccessException("Expected peer must belong to this user and interactive session.");
        var actual = new OsPipePeerVerifier(stream, serverSide).VerifyPeer();
        if (actual != expected) throw new UnauthorizedAccessException("OS pipe identity differs from the launched peer; launch nonce was not sent.");
        var authorization = new PipeAuthorization(expected.UserSid, expected.SessionId, expected.ProcessId,
            expected.ProcessStartTicks, launch.Nonce, new OsPipePeerVerifier(stream, serverSide));
        // Bidirectional hello; OS identity is checked before accepting the launch nonce.
        await Protocol.WriteFrameAsync(stream, Protocol.Encode(Hello(launch.Nonce)), cancellationToken);
        var hello = Protocol.Decode<PipeHello>(await Protocol.ReadFrameAsync(stream, cancellationToken));
        if (!authorization.Authenticate(hello)) throw new UnauthorizedAccessException("Pipe peer/launch authentication failed.");
    }
    public static async Task ServeCommandsAsync(PipeStream stream, int sessionId, IEffectsClient coordinator, CancellationToken cancellationToken,
        bool recoveryControl = false)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var writer = new SemaphoreSlim(1, 1);
        var pending = new ConcurrentDictionary<Guid, Task>();
        Exception? failure = null;
        async Task DispatchAsync(IpcRequest request)
        {
            try
            {
                // Reduce immediately on receipt, independently of disk/native awaits.
                // Thus a safety command can invalidate a previously queued write.
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var ack = await coordinator.SendAsync(request.Envelope, timeout.Token);
                await writer.WaitAsync(timeout.Token);
                try
                {
                    await Protocol.WriteFrameAsync(stream, Protocol.Encode(new IpcResponse(1, "result", request.Envelope.CommandId,
                        ack.Decision, ack.Completion, IpcState.From(ack.State))), timeout.Token);
                }
                finally { writer.Release(); }
                if (request.Envelope.Command is Shutdown) lifetime.Cancel();
            }
            catch (Exception error) { Interlocked.CompareExchange(ref failure, error, null); lifetime.Cancel(); }
        }
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var request = Protocol.Decode<IpcRequest>(await Protocol.ReadFrameAsync(stream, timeout.Token));
                if (request.Kind != "command" || request.SessionId != sessionId) throw new UnauthorizedAccessException("Wrong pipe session/route.");
                if (recoveryControl && request.Envelope.Command is not GetState and not Restore and not Shutdown)
                    throw new UnauthorizedAccessException("Recovery channel permits only state/liveness queries and safety restoration.");
                foreach (var completed in pending.Where(p => p.Value.IsCompleted).ToArray()) pending.TryRemove(completed.Key, out _);
                if (pending.Count >= 32 || pending.ContainsKey(request.Envelope.CommandId)) throw new ContractException("IPC command concurrency limit or duplicate command ID.");
                pending[request.Envelope.CommandId] = DispatchAsync(request);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { }
        finally
        {
            lifetime.Cancel();
            // Cancellation fences pending journal writes. A synchronous hung native
            // call is handled by the independent Recovery process and pinned handle.
            await Task.WhenAll(pending.Values);
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
