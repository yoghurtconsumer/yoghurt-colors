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
        Func<IAgentBroadcastSource>? injectedBroadcasts = null, Func<AutomationSettings, IAutomationInput>? injectedInput = null)
    {
        AbiAssertions.Verify(); WindowsDisplayInventoryApi.VerifyAbi();
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
        var modeJournal = new RecoveryJournalStore(Path.Combine(directory, "display-preview.json"));
        var oldMode = await modeJournal.LoadAsync(CancellationToken.None);
        if (oldMode is not null && oldMode.Status != JournalStatus.Restored)
            throw new InvalidOperationException("Unfinished display preview requires local recovery before starting a new writer.");
        using var worker = new AgentStaWorker();
        var inventory = runtime.Inventory();
        var coordinator = new JournaledEffectsCoordinator(worker, new EffectsState { Capabilities = Capabilities(inventory, launch.Services) },
            () => !ui.HasExited && !recovery.HasExited,
            injectedRuntime is null ? () => new DisplayInventoryReader(new WindowsDisplayInventoryApi()).Read() : null);
        using var owned = new OwnedResourceController(journal, new()
        { UserSid = own.UserSid, SessionId = own.SessionId, AgentPid = own.ProcessId, AgentStartTicks = own.StartTicks }, coordinator, () => lease.IsHeld);
        var catalog = runtime.CreateResources(inventory, launch.Services.GammaCompatibility, launch.Services.NvidiaExperimental);
        coordinator.AttachBackend(new PlannedColorBackend(owned, catalog.Resources, catalog.Matrix, () => coordinator.State, launch.Services));
        using var modeOwned = new OwnedResourceController(modeJournal, new()
        { UserSid = own.UserSid, SessionId = own.SessionId, AgentPid = own.ProcessId, AgentStartTicks = own.StartTicks }, coordinator.ModeSafetyGate, () => lease.IsHeld);
        if (launch.Services.DisplayPreviewExperimental && runtime.CreateDisplayMode is { } createMode)
            coordinator.AttachDisplayPreview(createMode(), modeOwned);
        using var lifetime = new CancellationTokenSource();
        await using var automation = new AutomationRuntime(coordinator, launch.Ui.ProcessId, coordinator.UpdateAutomationStatus, injectedInput);
        var automationTask = automation.RunAsync(lifetime.Token);
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
            await Task.WhenAny(uiCommands, recoveryCommands, uiExit, recoveryExit, automationTask);
            // Disconnect, UI death, companion death or shutdown all invalidate queued writes first.
            coordinator.RecoveryReady = false;
            var result = await SendSafetyAsync(new Shutdown());
            CompanionLaunch.Log("Agent", $"Shutdown restoration: {result.Completion.Success} {result.Completion.ErrorCode} {result.Completion.Explanation}");
            return result.Completion.Success ? 0 : 3;
        }
        finally
        {
            coordinator.RecoveryReady = false; lifetime.Cancel();
            try { await automationTask; } catch (Exception error) { CompanionLaunch.Log("Agent", "Input loop stopped: " + error.Message); }
            await automation.DisposeAsync();
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
        backends.RemoveAll(b => b.Backend == BackendKind.DisplayMode);
        backends.Add(new(BackendKind.DisplayMode, services.DisplayPreviewExperimental && inventory.AllSdr ? Capability.Experimental : Capability.NeedsUserAction,
            "Explicit experimental SDR source-size/scaling preview at current physical refresh; independent Recovery and 15-second Keep/Revert. No saved custom mode or HDR toggle.", EffectScope.GlobalDesktop));
        return new(backends.ToImmutableArray(), inventory.AdvancedColorActive || !inventory.Known);
    }
}
