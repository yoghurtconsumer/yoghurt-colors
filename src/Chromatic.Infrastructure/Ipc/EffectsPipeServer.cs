using System.IO.Pipes;
namespace Chromatic.Infrastructure.Ipc;

public static class EffectsPipeServer
{
    public const PipeOptions RequiredOptions = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;
    // No listener is exposed until PID/token/session verification and liveness ownership
    // are implemented together in Stage 2. CurrentUserOnly alone is insufficient.
    public static Task StartAsync(CancellationToken cancellationToken) =>
        Task.FromException(new NotSupportedException("NotImplemented: authenticated Agent IPC transport (Stage 2)."));
}
