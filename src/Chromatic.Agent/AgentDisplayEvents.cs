using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Native;

namespace Chromatic.Agent;

public sealed class AgentDisplayEvents(JournaledEffectsCoordinator coordinator, DisplaySafetyReport initial,
    Func<DisplaySafetyReport> readInventory, Func<DisplaySafetyReport, CapabilityReport> capabilities, Action<string> log)
{
    private DisplaySafetyReport _inventory = initial;
    public async Task HandleAsync(AgentBroadcast kind)
    {
        if (kind == AgentBroadcast.DisplayChanged)
        {
            var refreshed = readInventory();
            var preserve = DisplayChangePolicy.CanPreserveColor(coordinator.State, _inventory, refreshed);
            _inventory = refreshed;
            coordinator.UpdateCapabilities(capabilities(_inventory));
            if (preserve)
            {
                log("Display notification: same verified SDR output mappings; confirmed color route remains enabled. No setter or reapply was issued.");
                return;
            }
        }
        log($"OS notification: {kind}; effects require explicit Apply after a safety suspension.");
        if (kind is AgentBroadcast.SessionSuspended or AgentBroadcast.PowerSuspended or AgentBroadcast.DisplayChanged)
            await SendAsync(new Suspend(kind == AgentBroadcast.PowerSuspended ? SuspensionReason.PowerSuspend :
                kind == AgentBroadcast.SessionSuspended ? SuspensionReason.SessionLock : SuspensionReason.AdvancedColor));
        _inventory = readInventory(); coordinator.UpdateCapabilities(capabilities(_inventory));
        if (kind == AgentBroadcast.SessionUnlocked) await SendAsync(new Resume(SuspensionReason.SessionLock));
        if (kind == AgentBroadcast.PowerResumed) await SendAsync(new Resume(SuspensionReason.PowerSuspend));
        if (_inventory.AllSdr) await SendAsync(new Resume(SuspensionReason.AdvancedColor));
    }
    private async Task SendAsync(EffectsCommand command)
    {
        var state = coordinator.State;
        EffectsAcknowledgment acknowledgment = await coordinator.SendAsync(new(Guid.NewGuid(), state.Revision, state.Generation, command));
        if (!acknowledgment.Completion.Success)
            throw new InvalidOperationException($"OS safety transition failed: {acknowledgment.Completion.ErrorCode}: {acknowledgment.Completion.Explanation}");
    }
}
