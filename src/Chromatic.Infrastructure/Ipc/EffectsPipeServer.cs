using System.IO.Pipes;
using Chromatic.Core.Abstractions;
namespace Chromatic.Infrastructure.Ipc;

public static class EffectsPipeServer
{
    public const PipeOptions RequiredOptions = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;
    // Unconfigured callers never get an unauthenticated listener.
    public static Task StartAsync(CancellationToken cancellationToken) =>
        Task.FromException(new NotSupportedException("An OS-verified expected peer, launch identity and owned coordinator are required."));
    public static async Task StartAsync(NamedPipeServerStream stream, PipeLaunchIdentity launch, int sessionId,
        IEffectsClient coordinator, CancellationToken cancellationToken)
    {
        await AuthenticatedPipe.AcceptAsync(stream, launch, cancellationToken);
        await AuthenticatedPipe.ServeCommandsAsync(stream, sessionId, coordinator, cancellationToken);
    }
}
