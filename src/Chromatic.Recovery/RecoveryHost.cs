using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Effects;
using Chromatic.Infrastructure.Ipc;
using Chromatic.Infrastructure.Persistence;
using Chromatic.Native;

namespace Chromatic.Recovery;

public static class RecoveryHost
{
    public static async Task<int> RunAsync(CompanionBootstrap launch, CompanionRuntime? injectedRuntime = null)
    {
        AbiAssertions.Verify(); DisplaySafetyProbe.VerifyAbi();
        if (PipeInterop.Current != launch.Recovery) throw new UnauthorizedAccessException("Recovery identity mismatch.");
        var runtime = injectedRuntime ?? CompanionRuntime.Production;
        using var supervisorLease = new OwnedSessionLease(Path.Combine(runtime.SessionDirectory, "supervisor.lock"));
        var journal = new RecoveryJournalStore(Path.Combine(runtime.SessionDirectory, "recovery.json"));
        if (launch.RecoveryOnly)
        {
            if (!PriorAgentHasExited(launch.Agent)) throw new InvalidOperationException("The journal's Agent is still alive or cannot be verified.");
            return await RestoreOnStaAsync(runtime, journal, launch.Agent, () => PriorAgentHasExited(launch.Agent));
        }
        var agentImage = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Agent", "Chromatic.Agent.exe"));
        using var agent = new VerifiedProcess(launch.Agent, agentImage, ownAgentMayTerminate: true);
        using var server = PipeInterop.CreateCurrentUserLocalServer(launch.RecoveryPipe);
        await AuthenticatedPipe.AcceptAsync(server, new(launch.RecoveryPipe, launch.Nonce, CompanionLaunch.Peer(launch.Agent)), CancellationToken.None);
        try
        {
            while (!agent.HasExited)
            {
                using var heartbeat = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var id = Guid.NewGuid();
                await Protocol.WriteFrameAsync(server, Protocol.Encode(new IpcRequest(1, "command", launch.Agent.SessionId,
                    new(id, 0, 0, new GetState()))), heartbeat.Token);
                var response = Protocol.Decode<IpcResponse>(await Protocol.ReadFrameAsync(server, heartbeat.Token));
                if (response.CommandId != id) throw new IOException("Recovery heartbeat correlation failed.");
                var pendingMode = await new RecoveryJournalStore(Path.Combine(runtime.SessionDirectory, "display-preview.json")).LoadAsync(heartbeat.Token);
                if (pendingMode is { Status: not JournalStatus.Restored, Preview: { } preview } && preview.DeadlineUtc <= DateTimeOffset.UtcNow)
                    throw new IOException("Unfinished display preview exceeded its durable deadline; requesting shutdown before recovery takeover.");
                await Task.Delay(1000);
            }
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or ContractException)
        {
            CompanionLaunch.Log("Recovery", $"Agent heartbeat ended: {error.GetType().Name}.");
            if (!agent.HasExited)
            {
                // Request restore first, even if a prior response timed out. Do not
                // trust a late response as proof of death or release of ownership.
                try
                {
                    using var request = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    await Protocol.WriteFrameAsync(server, Protocol.Encode(new IpcRequest(1, "command", launch.Agent.SessionId,
                        new(Guid.NewGuid(), 0, 0, new Shutdown()))), request.Token);
                }
                catch (Exception requestError) when (requestError is IOException or OperationCanceledException)
                { CompanionLaunch.Log("Recovery", $"Restore request failed: {requestError.GetType().Name}."); }
                using var grace = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { await agent.WaitForExitAsync(grace.Token); }
                catch (OperationCanceledException) { agent.TerminateHungAgent(); }
            }
        }
        using var exitDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await agent.WaitForExitAsync(exitDeadline.Token);
        return await RestoreOnStaAsync(runtime, journal, launch.Agent, () => agent.HasExited);
    }
    public static bool PriorAgentHasExited(NativePeerIdentity expected)
    {
        try { return PipeInterop.ReadProcess(expected.ProcessId) != expected; }
        catch (Win32Exception error) when (error.NativeErrorCode == 87) { return true; } // PID no longer exists
        // Access denied, token failure etc. are not proof of termination.
    }
    private static Task<int> RestoreOnStaAsync(CompanionRuntime runtime, RecoveryJournalStore journal, NativePeerIdentity expected, Func<bool> exited)
    {
        var result = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            _ = dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    if (!exited()) throw new InvalidOperationException("Agent termination has not been verified.");
                    // The exclusive lease is acquired only AFTER the pinned process is signaled.
                    using var lease = new OwnedSessionLease(Path.Combine(runtime.SessionDirectory, "writer.lock"));
                    var modeJournal = new RecoveryJournalStore(Path.Combine(runtime.SessionDirectory, "display-preview.json"));
                    var modeSnapshot = await modeJournal.LoadAsync(CancellationToken.None);
                    var current = PipeInterop.Current;
                    if (modeSnapshot is not null && modeSnapshot.Status != JournalStatus.Restored)
                    {
                        if (modeSnapshot.AgentPid != expected.ProcessId || modeSnapshot.AgentStartTicks != expected.StartTicks || runtime.CreateDisplayMode is null)
                            throw new UnauthorizedAccessException("Mode journal does not match the verified Agent or no mode recovery adapter exists.");
                        var modeResult = await new RestoreExecutor(modeJournal, [runtime.CreateDisplayMode()], _ => exited(), () => lease.IsHeld,
                            current.UserSid, current.SessionId).RestoreAsync(modeSnapshot, CancellationToken.None);
                        CompanionLaunch.Log("Recovery", $"Display preview restore: {modeResult.Success} {modeResult.Explanation}");
                        if (!modeResult.Success) { result.SetResult(3); return; }
                    }
                    var snapshot = await journal.LoadAsync(CancellationToken.None);
                    if (snapshot is null || snapshot.Status == JournalStatus.Restored) { result.SetResult(0); return; }
                    if (snapshot.AgentPid != expected.ProcessId || snapshot.AgentStartTicks != expected.StartTicks)
                        throw new UnauthorizedAccessException("Journal does not belong to the verified Agent.");
                    using var catalog = runtime.CreateResources(runtime.Inventory(),
                        snapshot.Resources.Any(r => r.Kind == OwnedResourceKind.GammaRamp),
                        snapshot.Resources.Any(r => r.Kind == OwnedResourceKind.NativeVibrance));
                    var restored = await new RestoreExecutor(journal, catalog.Resources, _ => exited(), () => lease.IsHeld,
                        current.UserSid, current.SessionId).RestoreAsync(snapshot, CancellationToken.None);
                    CompanionLaunch.Log("Recovery", $"Baseline restoration: {restored.Success} {restored.ErrorCode} {restored.Explanation}");
                    result.SetResult(restored.Success ? 0 : 3);
                }
                catch (Exception error) { CompanionLaunch.Log("Recovery", error.ToString()); result.TrySetResult(3); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
            });
            Dispatcher.Run();
        }) { Name = "Chromatic recovery STA", IsBackground = false };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return result.Task;
    }
}
