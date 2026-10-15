using System.IO.Pipes;
using Chromatic.Native;

namespace Chromatic.Infrastructure.Ipc;
public sealed class OsPipePeerVerifier(PipeStream pipe, bool serverSide) : IPipePeerVerifier
{
    public VerifiedPipePeer VerifyPeer()
    {
        var peer = PipeInterop.ReadPeer(pipe, serverSide);
        // Server streams must originate from CreateCurrentUserLocalServer; clients
        // below always connect to '.', never a caller-provided remote host.
        return new(peer.ProcessId, peer.StartTicks, peer.SessionId, peer.UserSid, true);
    }
}
