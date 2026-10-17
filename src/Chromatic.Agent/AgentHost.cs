using System.Collections.Immutable;
using System.IO;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Effects;
using Chromatic.Infrastructure.Ipc;
using Chromatic.Infrastructure.Persistence;
using Chromatic.Native;
using Chromatic.Agent.Backends;

namespace Chromatic.Agent;

public static class AgentHost
{
    public static async Task<int> RunAsync(CompanionBootstrap launch, CompanionRuntime? injectedRuntime = null,
        Func<IAgentBroadcastSource>? injectedBroadcasts = null)
    {
        AbiAssertions.Verify(); DisplaySafetyProbe.VerifyAbi();
        if (launch.RecoveryOnly) throw new UnauthorizedAccessException("Recovery-only launch cannot start an Agent.");
        var own = PipeInterop.Current;
        if (own != launch.Agent) throw new UnauthorizedAccessException("Agent bootstrap identity mismatch.");
        using var ui = new VerifiedProcess(launch.Ui, launch.UiImage);
        var recoveryImage = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Recovery", "Chromatic.Recovery.exe"));
        using var recovery = new VerifiedProcess(launch.Recovery, recoveryImage);
        var runtime = injectedRuntime ?? CompanionRuntime.Production;
        var directory = runtime.SessionDirectory;
        using var lease = new OwnedSessionLease(Path.Combine(directory, "writer.lock"));
        var journal = new RecoveryJournalStore(Path.Combine(directory, "recovery.json"));
        var old = await journal.LoadAsync(CancellationToken.None);
        if (old is not null && old.Status != JournalStatus.Restored)
            throw new InvalidOperationException("Unfinished recovery journal requires local review before effects can be enabled.");
        using var worker = new AgentStaWorker();
        var inventory = runtime.Inventory();
        var coordinator = new JournaledEffectsCoordinator(worker, new EffectsState { Capabilities = Capabilities(inventory, launch.Services) },
            () => !ui.HasExited && !recovery.HasExited);
        using var owned = new OwnedResourceController(journal, new()
        { UserSid = own.UserSid, SessionId = own.SessionId, AgentPid = own.ProcessId, AgentStartTicks = own.StartTicks }, coordinator, () => lease.IsHeld);
        var catalog = runtime.CreateResources(inventory, launch.Services.GammaCompatibility, launch.Services.NvidiaExperimental);
        coordinator.AttachBackend(new PlannedColorBackend(owned, catalog.Resources, catalog.Matrix, () => coordinator.State, launch.Services));
        using var lifetime = new CancellationTokenSource();
        // Tests inject their event source alongside file-backed resources. The
        // production entry point always installs the real Windows notifications;
        // there is no bootstrap/IPC option to turn those safety events off.
        var broadcast = await worker.InvokeAsync<IAgentBroadcastSource>(() => injectedBroadcasts?.Invoke() ?? new HiddenBroadcastWindow());
        var displayEvents = new AgentDisplayEvents(coordinator, inventory, runtime.Inventory,
            report => Capabilities(report, launch.Services), message => CompanionLaunch.Log("Agent", message));
        broadcast.Broadcast += kind => _ = HandleBroadcastAsync(kind);
        async Task HandleBroadcastAsync(AgentBroadcast kind)
        {
            try
            {
                await displayEvents.HandleAsync(kind);
            }
            catch (Exception error) { coordinator.RecoveryReady = false; CompanionLaunch.Log("Agent", error.Message); lifetime.Cancel(); }
        }
        Task<Chromatic.Core.Abstractions.EffectsAcknowledgment> SendSafetyAsync(EffectsCommand command) =>
            coordinator.SendAsync(new(Guid.NewGuid(), coordinator.State.Revision, coordinator.State.Generation, command));
        using var uiPipe = PipeInterop.CreateCurrentUserLocalServer(launch.AgentPipe);
        try
        {
            using var recoveryPipe = await AuthenticatedPipe.ConnectAsync(new(launch.RecoveryPipe, launch.Nonce, CompanionLaunch.Peer(launch.Recovery)), lifetime.Token);
            var recoveryCommands = AuthenticatedPipe.ServeCommandsAsync(recoveryPipe, own.SessionId, coordinator, lifetime.Token, recoveryControl: true);
            coordinator.RecoveryReady = true; // OS-authenticated, pinned recovery process and live pipe now exist.
            var uiCommands = EffectsPipeServer.StartAsync(uiPipe, new(launch.AgentPipe, launch.Nonce, CompanionLaunch.Peer(launch.Ui)),
                own.SessionId, coordinator, lifetime.Token);
            var uiExit = ui.WaitForExitAsync(lifetime.Token);
            var recoveryExit = recovery.WaitForExitAsync(lifetime.Token);
            await Task.WhenAny(uiCommands, recoveryCommands, uiExit, recoveryExit);
            // Disconnect, UI death, companion death or shutdown all invalidate queued writes first.
            coordinator.RecoveryReady = false;
            var result = await SendSafetyAsync(new Shutdown());
            CompanionLaunch.Log("Agent", $"Shutdown restoration: {result.Completion.Success} {result.Completion.ErrorCode} {result.Completion.Explanation}");
            return result.Completion.Success ? 0 : 3;
        }
        finally
        {
            coordinator.RecoveryReady = false; lifetime.Cancel();
            uiPipe.Dispose();
            await worker.InvokeAsync(() => { broadcast.Dispose(); catalog.Dispose(); return true; }, true);
        }
    }
    private static CapabilityReport Capabilities(DisplaySafetyReport inventory, OptionalServices services)
    {
        var backends = CapabilityReport.Stage1.Backends.Where(b => b.Backend is not BackendKind.Magnification and not BackendKind.SdrGamma and not BackendKind.NvidiaDvc).ToList();
        backends.Add(new(BackendKind.Magnification, inventory.AllSdr ? Capability.Available : Capability.Unsupported,
            inventory.Reason + (inventory.AdvancedColorActive ? " To use saturation, switch off HDR/automatic color management yourself in Windows Display settings, then return and press Apply." : "") +
            " Native API success is reported only after explicit Apply and readback.", EffectScope.GlobalDesktop));
        foreach (var display in inventory.Displays)
        {
            backends.Add(new(BackendKind.SdrGamma, services.GammaCompatibility && inventory.AllSdr ? Capability.Available : Capability.NeedsUserAction,
                "Optional LUT; driver acceptance and latency require qualification. Hold is unavailable until measured.", EffectScope.PerMonitor, display));
            backends.Add(new(BackendKind.NvidiaDvc, services.NvidiaExperimental && inventory.AllSdr ? Capability.Experimental : Capability.NeedsUserAction,
                "Private driver ABI; explicit single output and opt-in required. Unsupported outputs fall back to software.", EffectScope.PerMonitor, display));
        }
        return new(backends.ToImmutableArray(), inventory.AdvancedColorActive || !inventory.Known);
    }
}
