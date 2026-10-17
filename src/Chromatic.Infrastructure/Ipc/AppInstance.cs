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
    public static string PipeName(NativePeerIdentity identity, string scope = "Chromatic.App") => "ChromaticDesktop." + identity.SessionId + "." +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.UserSid + "|" + scope)))[..32].ToLowerInvariant();
    public static string? ParseArguments(string[] args)
    {
        if (args.Length == 0) return null;
        if (args.Length != 1) throw new ContractException("Expected no arguments or one chromaticdesk://preset/… link.");
        _ = PresetExchange.ParseLink(args[0]); return args[0];
    }
    public static async Task<AppInstance?> StartOrForwardAsync(string image, string? link, Action<string?> received, string scope = "Chromatic.App")
    {
        if (link is not null) _ = PresetExchange.ParseLink(link);
        // Production always uses the default product scope. Tests inject an
        // isolated scope, never sharing a user's real application endpoint.
        var name = PipeName(PipeInterop.Current, scope);
        try { return new(PipeInterop.CreateCurrentUserLocalServer(name), image, received); }
        catch (Win32Exception e) when (e.NativeErrorCode is 5 or 231)
        {
            try
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
                // Confirm that the acknowledgment was consumed, then wait for
                // server disconnect before another launch can reuse this pipe.
                await WriteAsync(client, new(1, "accepted", challenge.Challenge), timeout.Token);
                if (await client.ReadAsync(new byte[1], timeout.Token) != 0)
                    throw new ContractException("Unexpected trailing handoff data.");
                return null;
            }
            catch (OperationCanceledException error)
            {
                throw new TimeoutException("Could not contact an existing Chromatic Desktop copy. Exit the older copy before opening this build. If its window has already closed, end only the remaining Chromatic.App process in Task Manager, then retry.", error);
            }
            catch (UnauthorizedAccessException error)
            {
                throw new UnauthorizedAccessException("Another Chromatic Desktop copy is running from a different folder or has an unverified identity. Exit the older copy before opening this build. This build did not start a second UI or Agent.", error);
            }
        }
    }
    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            var accepted = false;
            try
            {
                await _server.WaitForConnectionAsync(_stop.Token);
                accepted = true;
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
                // Disconnect discards unread pipe data even after WriteAsync
                // completes. Wait for the client's bounded receipt confirmation.
                var receipt = await ReadAsync(_server, timeout.Token);
                if (receipt.Kind != "accepted" || receipt.Challenge != challenge || receipt.Link is not null || peer.HasExited)
                    throw new UnauthorizedAccessException("Handoff receipt mismatch.");
            }
            catch (Exception e) when (e is IOException or ContractException or UnauthorizedAccessException or Win32Exception or OperationCanceledException or ArgumentException or JsonException or InvalidOperationException)
            { /* Reject this peer; keep the primary application and exclusive pipe alive. */ }
            // A peer that closes early marks the stream Broken, for which
            // IsConnected is false. Disconnect still must reset that instance.
            finally { if (accepted) _server.Disconnect(); }
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
        // WPF OnExit synchronously waits for this disposal. Never post its
        // continuation back to that blocked UI thread or retain the pipe forever.
        try { await _serve.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        finally { _server.Dispose(); _stop.Dispose(); }
    }
}
