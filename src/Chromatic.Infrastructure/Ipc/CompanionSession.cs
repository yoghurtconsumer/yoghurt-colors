using System.Diagnostics;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Persistence;
using Chromatic.Native;

namespace Chromatic.Infrastructure.Ipc;

public sealed class CompanionSession : IEffectsClient, IDisposable
{
    private EffectsPipeClient _client = new();
    private Process? _agent, _recovery;
    private readonly CancellationTokenSource _lifetime = new();
    public string? Failure { get; private set; }
    public EffectsState State => _client.State;
    public bool Connected => _client.Connected;
    public async Task StartAsync(OptionalServices services, Func<RecoverySnapshot, bool> offerRecovery)
    {
        try
        {
            var own = PipeInterop.Current;
            var uiImage = Environment.ProcessPath ?? throw new IOException("UI image path unavailable.");
            var previous = await new RecoveryJournalStore(Path.Combine(CompanionLaunch.SessionDirectory, "recovery.json")).LoadAsync(_lifetime.Token);
            var previousMode = await new RecoveryJournalStore(Path.Combine(CompanionLaunch.SessionDirectory, "display-preview.json")).LoadAsync(_lifetime.Token);
            if (previousMode is not null && previousMode.Status != JournalStatus.Restored)
            {
                if (previous is not null && previous.Status != JournalStatus.Restored &&
                    (previous.AgentPid != previousMode.AgentPid || previous.AgentStartTicks != previousMode.AgentStartTicks))
                    throw new InvalidOperationException("Color and mode journals belong to different Agent owners; local review is required.");
                previous = previousMode;
            }
            if (previous is not null && previous.Status != JournalStatus.Restored)
            {
                if (previous.UserSid != own.UserSid || previous.SessionId != own.SessionId || !offerRecovery(previous))
                    throw new InvalidOperationException("Unfinished recovery journal preserved; effects remain disconnected.");
                using var recovery = CompanionLaunch.Start(CompanionLaunch.Image("Recovery"));
                await CompanionLaunch.SendAsync(recovery, new(1, own, uiImage,
                    new(previous.AgentPid, previous.AgentStartTicks, previous.SessionId, previous.UserSid), PipeInterop.ReadProcess(recovery.Id),
                    PipeAuthorization.CreatePipeName(own.SessionId), PipeAuthorization.CreatePipeName(own.SessionId), PipeAuthorization.CreateNonce(), services, true));
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                await recovery.WaitForExitAsync(deadline.Token);
                if (recovery.ExitCode != 0) throw new InvalidOperationException("Recovery declined or failed. Review the recovery log; no effects were enabled.");
            }
            _agent = CompanionLaunch.Start(CompanionLaunch.Image("Agent"));
            _recovery = CompanionLaunch.Start(CompanionLaunch.Image("Recovery"));
            var bootstrap = new CompanionBootstrap(1, own, uiImage, PipeInterop.ReadProcess(_agent.Id), PipeInterop.ReadProcess(_recovery.Id),
                PipeAuthorization.CreatePipeName(own.SessionId), PipeAuthorization.CreatePipeName(own.SessionId), PipeAuthorization.CreateNonce(), services);
            await CompanionLaunch.SendAsync(_recovery, bootstrap);
            await CompanionLaunch.SendAsync(_agent, bootstrap);
            var pipe = await AuthenticatedPipe.ConnectAsync(new(bootstrap.AgentPipe, bootstrap.Nonce, CompanionLaunch.Peer(bootstrap.Agent)), _lifetime.Token);
            _client.Dispose(); _client = new(pipe);
            await _client.QueryAsync(_lifetime.Token);
            _ = HeartbeatAsync();
        }
        catch (Exception error)
        {
            Failure = $"Agent unavailable: {error.Message}";
            CompanionLaunch.Log("App", error.ToString()); _client.Dispose();
            // Closing bootstrap/IPC and the UI process handle let companions restore
            // independently. Never kill a possibly writing child here.
            CloseInput(_agent); CloseInput(_recovery);
        }
    }
    private async Task HeartbeatAsync()
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                await Task.Delay(1000, _lifetime.Token);
                var ack = await _client.QueryAsync(_lifetime.Token);
                if (!_client.Connected || !ack.Completion.Success) throw new IOException("Agent heartbeat is disconnected.");
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error) { Failure = $"Agent connection ended: {error.Message}"; CompanionLaunch.Log("App", Failure); _client.Dispose(); }
    }
    public async Task<EffectsAcknowledgment> SendAsync(CommandEnvelope command, CancellationToken cancellationToken = default)
    {
        try { return await _client.SendAsync(command, cancellationToken); }
        catch (Exception error)
        {
            Failure = $"Agent request failed: {error.Message}";
            return new(new(CompletionStatus.Blocked, State.Revision, State.Generation, Failure), State,
                new(false, "AgentPipe", "Disconnected", Failure, State.Revision));
        }
    }
    private static void CloseInput(Process? process)
    {
        try { process?.StandardInput.Close(); } catch (InvalidOperationException) { } catch (IOException) { }
    }
    public void Dispose()
    {
        _lifetime.Cancel(); _client.Dispose(); CloseInput(_agent); CloseInput(_recovery);
        _agent?.Dispose(); _recovery?.Dispose();
    }
}
