using System.Buffers.Binary;
using System.Text.Json;
using System.Collections.Immutable;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Persistence;

namespace Chromatic.Infrastructure.Ipc;

public sealed record PipeHello(int SchemaVersion, string Kind, int ProcessId, long ProcessStartTicks,
    int SessionId, string UserSid, string Nonce);
public sealed record IpcRequest(int SchemaVersion, string Kind, int SessionId, CommandEnvelope Envelope);
public sealed record IpcState(long Revision, long Generation, bool DesiredEnabled, bool EffectiveEnabled,
    bool Held, string ActiveProfileId, ColorSettings GlobalColor, ImmutableArray<MonitorColorSettings> Monitors,
    PreviewTransaction? Preview, bool RecoveryPending, ImmutableArray<SuspensionReason> Suspensions,
    CapabilityReport Capabilities, ApplyResult? LastObservedResult)
{
    public EffectsState ToEffectsState() => new()
    {
        Revision = Revision, Generation = Generation, Enabled = DesiredEnabled, Held = Held,
        DesktopProfile = new() { Id = ActiveProfileId, GlobalColor = GlobalColor, Monitors = Monitors },
        Preview = Preview, RecoveryPending = RecoveryPending, Suspensions = Suspensions.ToImmutableHashSet(),
        Capabilities = Capabilities, Observed = new(Revision, Generation, LastObservedResult)
    };
    public static IpcState From(EffectsState state) => new(state.Revision, state.Generation, state.Enabled,
        state.Effective.Enabled, state.Held, state.BaseProfile.Id, state.Effective.GlobalColor,
        state.Effective.Monitors, state.Preview, state.RecoveryPending,
        state.Suspensions.OrderBy(s => s).ToImmutableArray(), state.Capabilities, state.Observed.LastResult);
}
public sealed record IpcResponse(int SchemaVersion, string Kind, Guid CommandId,
    CommandResult Decision, ApplyResult Completion, IpcState State);

public static class Protocol
{
    public const int Version = 1;
    public const int MaxFrameBytes = 65_536;
    public static byte[] Encode<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonContract.Options);
        using var doc = JsonContract.Parse(bytes, MaxFrameBytes);
        JsonContract.Validate("ipc", doc.RootElement);
        return bytes;
    }
    public static T Decode<T>(ReadOnlyMemory<byte> bytes)
    {
        using var doc = JsonContract.Parse(bytes, MaxFrameBytes);
        JsonContract.Validate("ipc", doc.RootElement);
        return doc.RootElement.Deserialize<T>(JsonContract.Options) ?? throw new ContractException("Null IPC frame.");
    }
    public static async Task WriteFrameAsync(Stream stream, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        if (frame.Length is < 1 or > MaxFrameBytes) throw new ContractException("Invalid IPC frame size.");
        var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, frame.Length);
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(frame, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
    public static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var size = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (size is < 1 or > MaxFrameBytes) throw new ContractException("Invalid IPC frame size.");
        var frame = new byte[size];
        await stream.ReadExactlyAsync(frame, cancellationToken);
        return frame;
    }
}
