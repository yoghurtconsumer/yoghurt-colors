using System.Buffers.Binary;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Ipc;
using Chromatic.Infrastructure.Persistence;
using Xunit;
namespace Chromatic.Integration.Tests;
public sealed class IpcTests
{
    [Fact] public void CompletionResponseCarriesAuthoritativeStateAndRealFailure()
    {
        var state = new EffectsState { Revision = 12, Generation = 15 };
        var response = new IpcResponse(1, "result", Guid.NewGuid(),
            new(CompletionStatus.NotImplemented, 12, 15, "Stage 1"), ApplyResult.NotImplemented("Native", 12), IpcState.From(state));
        var decoded = Protocol.Decode<IpcResponse>(Protocol.Encode(response));
        Assert.Equal(12, decoded.State.Revision); Assert.Equal(15, decoded.State.Generation);
        Assert.False(decoded.Completion.Success); Assert.Equal("NotImplemented", decoded.Completion.ErrorCode);
        Assert.Equal("desktop", decoded.State.ActiveProfileId);
    }
    [Fact] public void TypedCommandsRoundTripWithoutReflectionDispatch()
    {
        EffectsCommand[] commands = [new Enable(true), new SetColor(new(200)), new ActivatePreset(new()),
            new ForegroundChanged(null, null), new SetHold(false),
            new PreviewMode(Guid.NewGuid(), new("monitor", null, "DISPLAY1", 1, 1), new(1920, 1080, new(60, 1)), DateTimeOffset.UtcNow),
            new ConfirmMode(Guid.NewGuid()), new CancelPreview(Guid.NewGuid()), new Suspend(SuspensionReason.SessionLock),
            new Resume(SuspensionReason.SessionLock), new Restore(), new Shutdown(), new GetState()];
        foreach (var command in commands)
        {
            var request = new IpcRequest(1, "command", 1, new(Guid.NewGuid(), 4, 8, command));
            var decoded = Protocol.Decode<IpcRequest>(Protocol.Encode(request));
            Assert.Equal(command.GetType(), decoded.Envelope.Command.GetType());
            Assert.Equal(request.Envelope.CommandId, decoded.Envelope.CommandId);
        }
    }
    [Fact] public async Task FramesAreBoundedAndTruncationIsExplicit()
    {
        await using var stream = new MemoryStream();
        var payload = Protocol.Encode(new PipeHello(1, "hello", 10, 100, 1, "S-1-5-21-1", new string('a', 64)));
        await Protocol.WriteFrameAsync(stream, payload, default); stream.Position = 0;
        Assert.Equal(payload, await Protocol.ReadFrameAsync(stream, default));
        var badHeader = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(badHeader, 65_537);
        await Assert.ThrowsAsync<ContractException>(() => Protocol.ReadFrameAsync(new MemoryStream(badHeader), default));
        BinaryPrimitives.WriteInt32LittleEndian(badHeader, 100);
        await Assert.ThrowsAsync<EndOfStreamException>(() => Protocol.ReadFrameAsync(new MemoryStream(badHeader), default));
    }
    [Fact] public void MaliciousCommandAndOverlargePayloadAreRejected()
    {
        var raw = System.Text.Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"kind\":\"command\",\"sessionId\":1,\"envelope\":{\"commandId\":\"00000000-0000-0000-0000-000000000001\",\"baseRevision\":0,\"generation\":0,\"command\":{\"type\":\"shell\",\"path\":\"calc.exe\"}}}");
        Assert.Throws<ContractException>(() => Protocol.Decode<IpcRequest>(raw));
        Assert.Throws<ContractException>(() => Protocol.Decode<IpcRequest>(new byte[65_537]));
    }
    private sealed class Peer(VerifiedPipePeer identity) : IPipePeerVerifier { public VerifiedPipePeer VerifyPeer() => identity; }
    [Fact] public void AuthenticationChecksOsPeerAndRejectsReplay()
    {
        var nonce = PipeAuthorization.CreateNonce(); var identity = new VerifiedPipePeer(10, 100, 1, "S-1-5-21-1", true);
        var auth = new PipeAuthorization(identity.UserSid, 1, 10, 100, nonce, new Peer(identity));
        var hello = new PipeHello(1, "hello", 10, 100, 1, identity.UserSid, nonce);
        Assert.False(auth.Authenticate(hello with { Nonce = new string('b', 64) }));
        Assert.False(auth.Authenticate(hello with { SessionId = 2 }));
        Assert.False(auth.Authenticate(hello with { ProcessStartTicks = 101 }));
        Assert.True(auth.Authenticate(hello)); Assert.False(auth.Authenticate(hello));
    }
    [Theory] [InlineData(false, "S-1-5-21-1", 1, 10, 100)] [InlineData(true, "S-1-5-21-2", 1, 10, 100)]
    [InlineData(true, "S-1-5-21-1", 2, 10, 100)] [InlineData(true, "S-1-5-21-1", 1, 11, 100)]
    [InlineData(true, "S-1-5-21-1", 1, 10, 101)]
    public void ForgedHelloCannotOverrideOsIdentity(bool local, string sid, int session, int pid, long start)
    {
        var nonce = PipeAuthorization.CreateNonce();
        var auth = new PipeAuthorization("S-1-5-21-1", 1, 10, 100, nonce, new Peer(new(pid, start, session, sid, local)));
        Assert.False(auth.Authenticate(new(1, "hello", 10, 100, 1, "S-1-5-21-1", nonce)));
    }
#if !CHROMATIC_PORTABLE_PROBE
    // The historical probe deliberately excludes native OS transport assemblies.
    [Fact] public async Task UnconfiguredTransportFailsClosed()
    {
        Assert.Throws<NotSupportedException>(() => new UnavailablePipePeerVerifier().VerifyPeer());
        await Assert.ThrowsAsync<NotSupportedException>(() => EffectsPipeServer.StartAsync(default));
        using var client = new EffectsPipeClient();
        var ack = await client.SendAsync(new(Guid.NewGuid(), 0, 0, new Enable(true)));
        Assert.False(ack.Completion.Success); Assert.Equal("Disconnected", ack.Completion.ErrorCode); Assert.False(client.State.Enabled);
    }
#endif
}
