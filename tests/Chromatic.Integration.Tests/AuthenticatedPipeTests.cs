using Chromatic.Infrastructure.Ipc;
using Chromatic.Native;
using Xunit;
using Chromatic.Core.State;
using Chromatic.Agent;

namespace Chromatic.Integration.Tests;
public sealed class AuthenticatedPipeTests
{
    [Theory]
    [InlineData(SuspensionReason.SessionLock)]
    [InlineData(SuspensionReason.PowerSuspend)]
    [InlineData(SuspensionReason.AdvancedColor)]
    public async Task UiCannotClearOsSafetySuspensions(SuspensionReason reason)
    {
        var peer = Self(); var launch = new PipeLaunchIdentity(PipeAuthorization.CreatePipeName(peer.SessionId), PipeAuthorization.CreateNonce(), peer);
        using var server = PipeInterop.CreateCurrentUserLocalServer(launch.PipeName);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var accepting = AuthenticatedPipe.AcceptAsync(server, launch, timeout.Token);
        using var client = await AuthenticatedPipe.ConnectAsync(launch, timeout.Token); await accepting;
        using var worker = new AgentStaWorker(); var coordinator = new JournaledEffectsCoordinator(worker);
        var serving = AuthenticatedPipe.ServeCommandsAsync(server, peer.SessionId, coordinator, timeout.Token);
        await Protocol.WriteFrameAsync(client, Protocol.Encode(new IpcRequest(1, "command", peer.SessionId,
            new(Guid.NewGuid(), 0, 0, new Resume(reason)))), timeout.Token);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => serving);
        Assert.Equal(0, coordinator.State.Revision);
    }
    [Fact] public async Task RecoveryControlChannelCannotApplyColors()
    {
        var peer = Self(); var launch = new PipeLaunchIdentity(PipeAuthorization.CreatePipeName(peer.SessionId), PipeAuthorization.CreateNonce(), peer);
        using var server = PipeInterop.CreateCurrentUserLocalServer(launch.PipeName);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var accepting = AuthenticatedPipe.AcceptAsync(server, launch, timeout.Token);
        using var client = await AuthenticatedPipe.ConnectAsync(launch, timeout.Token); await accepting;
        using var worker = new AgentStaWorker(); var coordinator = new JournaledEffectsCoordinator(worker);
        var serving = AuthenticatedPipe.ServeCommandsAsync(server, peer.SessionId, coordinator, timeout.Token, recoveryControl: true);
        await Protocol.WriteFrameAsync(client, Protocol.Encode(new IpcRequest(1, "command", peer.SessionId,
            new(Guid.NewGuid(), 0, 0, new Enable(true)))), timeout.Token);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => serving);
        Assert.Equal(0, coordinator.State.Revision); Assert.False(coordinator.State.Enabled);
    }
    private static VerifiedPipePeer Self()
    {
        var self = PipeInterop.Current;
        return new(self.ProcessId, self.StartTicks, self.SessionId, self.UserSid, true);
    }
    [Fact] public async Task RealLocalPipeAuthenticatesBothOsPeersAndNonce()
    {
        var identity = Self(); var name = PipeAuthorization.CreatePipeName(identity.SessionId);
        var launch = new PipeLaunchIdentity(name, PipeAuthorization.CreateNonce(), identity);
        using var server = PipeInterop.CreateCurrentUserLocalServer(name);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var accepting = AuthenticatedPipe.AcceptAsync(server, launch, deadline.Token);
        using var client = await AuthenticatedPipe.ConnectAsync(launch, deadline.Token);
        await accepting;
        Assert.Equal(identity.ProcessId, PipeInterop.ReadPeer(server, true).ProcessId);
        Assert.Equal(identity.UserSid, PipeInterop.ReadPeer(client, false).UserSid);
    }
    [Fact] public async Task WrongLaunchNonceCannotAuthenticateEvenTheSameUser()
    {
        var identity = Self(); var name = PipeAuthorization.CreatePipeName(identity.SessionId);
        var launch = new PipeLaunchIdentity(name, PipeAuthorization.CreateNonce(), identity);
        using var server = PipeInterop.CreateCurrentUserLocalServer(name);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var accepting = AuthenticatedPipe.AcceptAsync(server, launch, deadline.Token);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        { using var client = await AuthenticatedPipe.ConnectAsync(launch with { Nonce = PipeAuthorization.CreateNonce() }, deadline.Token); });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => accepting);
    }
    [Fact] public async Task ForgedProcessStartTimeIsRejectedBeforeExchangingNonce()
    {
        var identity = Self(); var name = PipeAuthorization.CreatePipeName(identity.SessionId);
        var launch = new PipeLaunchIdentity(name, PipeAuthorization.CreateNonce(), identity);
        using var server = PipeInterop.CreateCurrentUserLocalServer(name);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var accepting = AuthenticatedPipe.AcceptAsync(server, launch, deadline.Token);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        { using var client = await AuthenticatedPipe.ConnectAsync(launch with { ExpectedPeer = identity with { ProcessStartTicks = identity.ProcessStartTicks + 1 } }, deadline.Token); });
        // Client closes before disclosing a nonce; server cannot accept a hello.
        await Assert.ThrowsAnyAsync<IOException>(() => accepting);
    }
}
