using System.Security.Cryptography;
using System.Text;

namespace Chromatic.Infrastructure.Ipc;

public sealed record VerifiedPipePeer(int ProcessId, long ProcessStartTicks, int SessionId, string UserSid, bool IsLocal);
// Production implementation must obtain peer PID from the OS pipe handle and validate
// its process token, start time and interactive session. A hello is never proof of identity.
public interface IPipePeerVerifier { VerifiedPipePeer VerifyPeer(); }
public sealed class UnavailablePipePeerVerifier : IPipePeerVerifier
{
    public VerifiedPipePeer VerifyPeer() => throw new NotSupportedException("NotImplemented: OS peer verification is required before opening IPC.");
}
public sealed class PipeAuthorization(string expectedSid, int expectedSession, int expectedPid,
    long expectedStartTicks, string launchNonce, IPipePeerVerifier peerVerifier)
{
    private int _authenticated;
    public static string CreateNonce() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    public static string CreatePipeName(int sessionId) => $"ChromaticDesktop.{sessionId}.{Guid.NewGuid():N}";
    public bool Authenticate(PipeHello hello)
    {
        if (Volatile.Read(ref _authenticated) != 0) return false;
        var peer = peerVerifier.VerifyPeer();
        if (hello.SchemaVersion != 1 || hello.Kind != "hello" || !peer.IsLocal ||
            peer.UserSid != expectedSid || peer.SessionId != expectedSession ||
            peer.ProcessId != expectedPid || peer.ProcessStartTicks != expectedStartTicks ||
            hello.UserSid != peer.UserSid || hello.SessionId != peer.SessionId ||
            hello.ProcessId != peer.ProcessId || hello.ProcessStartTicks != peer.ProcessStartTicks ||
            launchNonce.Length != 64 || hello.Nonce.Length != 64 ||
            !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(launchNonce), Encoding.ASCII.GetBytes(hello.Nonce))) return false;
        return Interlocked.CompareExchange(ref _authenticated, 1, 0) == 0;
    }
}
