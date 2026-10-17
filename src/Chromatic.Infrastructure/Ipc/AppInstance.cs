using System.ComponentModel;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chromatic.Infrastructure.Persistence;
using Chromatic.Native;

namespace Chromatic.Infrastructure.Ipc;

public sealed record AppHandoff(int SchemaVersion, string Kind, string Challenge, string? Link = null);

// This channel accepts only Show or a validated preset document. It has no effects
// or native command surface. Both endpoints pin the actual OS peer and app image.
public sealed class AppInstance : IAsyncDisposable
{
    private readonly NamedPipeServerStream _server;
    private readonly CancellationTokenSource _stop = new();
    private readonly string _image;
    private readonly Action<string?> _received;
    private readonly Task _serve;
    private AppInstance(NamedPipeServerStream server, string image, Action<string?> received)
    { _server = server; _image = image; _received = received; _serve = Task.Run(ServeAsync); }
    public static string PipeName(string image, NativePeerIdentity identity) => "ChromaticDesktop." + identity.SessionId + "." +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.UserSid + "|" + Path.GetFullPath(image).ToUpperInvariant())))[..32].ToLowerInvariant();
    public static string? ParseArguments(string[] args)
    {
        if (args.Length == 0) return null;
        if (args.Length != 1) throw new ContractException("Expected no arguments or one chromaticdesk://preset/… link.");
        _ = PresetExchange.ParseLink(args[0]); return args[0];
    }
    public static async Task<AppInstance?> StartOrForwardAsync(string image, string? link, Action<string?> received)
    {
        if (link is not null) _ = PresetExchange.ParseLink(link);
        var name = PipeName(image, PipeInterop.Current);
        try { return new(PipeInterop.CreateCurrentUserLocalServer(name), image, received); }
        catch (Win32Exception e) when (e.NativeErrorCode is 5 or 231)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(timeout.Token);
            using var peer = Authenticate(client, false, image);
            var challenge = await ReadAsync(client, timeout.Token);
            if (challenge.Kind != "challenge" || challenge.Link is not null) throw new UnauthorizedAccessException("Invalid handoff challenge.");
            await WriteAsync(client, new(1, "request", challenge.Challenge, link), timeout.Token);
            var ack = await ReadAsync(client, timeout.Token);
            if (ack.Kind != "accepted" || ack.Challenge != challenge.Challenge || ack.Link is not null || peer.HasExited)
                throw new UnauthorizedAccessException("Handoff acknowledgment mismatch.");
            return null;
        }
    }
    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await _server.WaitForConnectionAsync(_stop.Token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token); timeout.CancelAfter(TimeSpan.FromSeconds(3));
                using var peer = Authenticate(_server, true, _image);
                var challenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                await WriteAsync(_server, new(1, "challenge", challenge), timeout.Token);
                var request = await ReadAsync(_server, timeout.Token);
                if (request.Kind != "request" || request.Challenge != challenge || peer.HasExited)
                    throw new UnauthorizedAccessException("Invalid handoff request.");
                if (request.Link is not null) _ = PresetExchange.ParseLink(request.Link);
                _received(request.Link);
                await WriteAsync(_server, new(1, "accepted", challenge), timeout.Token);
            }
            catch (Exception e) when (e is IOException or ContractException or UnauthorizedAccessException or Win32Exception or OperationCanceledException or ArgumentException or JsonException or InvalidOperationException)
            { /* Reject this peer; keep the primary application and exclusive pipe alive. */ }
            finally { if (_server.IsConnected) _server.Disconnect(); }
        }
    }
    public static VerifiedProcess Authenticate(PipeStream pipe, bool serverSide, string image) => new(PipeInterop.ReadPeer(pipe, serverSide), image);
    private static async Task WriteAsync(Stream stream, AppHandoff message, CancellationToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonContract.Options);
        Validate(bytes); await Protocol.WriteFrameAsync(stream, bytes, token);
    }
    private static AppHandoff Validate(ReadOnlyMemory<byte> bytes)
    {
        using var json = JsonContract.Parse(bytes, PresetExchange.MaxCodeLength + 1024);
        JsonContract.Validate("handoff", json.RootElement);
        return json.RootElement.Deserialize<AppHandoff>(JsonContract.Options) ?? throw new ContractException("Missing handoff.");
    }
    private static async Task<AppHandoff> ReadAsync(Stream stream, CancellationToken token) => Validate(await Protocol.ReadFrameAsync(stream, token));
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        try { await _serve; } catch (OperationCanceledException) { }
        _server.Dispose(); _stop.Dispose();
    }
}
