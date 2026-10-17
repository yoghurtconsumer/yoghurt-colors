using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Effects;
using Chromatic.Infrastructure.Ipc;

namespace Chromatic.Agent;

// Accept reductions immediately, independently of a slow writer. Queued operations
// recheck the resulting generation after disk flush, immediately before mutation.
public sealed class JournaledEffectsCoordinator : IEffectsClient, IWriteSafetyGate
{
    private readonly object _stateLock = new();
    private readonly AgentStaWorker _worker;
    private IColorBackend? _backend;
    private readonly Func<DisplayInventorySnapshot>? _readDisplays;
    private DisplayInventorySnapshot? _displaySnapshot;
    private int _displayEpoch, _snapshotEpoch;
    public void InvalidateDisplayInventory() => Interlocked.Increment(ref _displayEpoch);
    private EffectsState _state;
    private volatile bool _recoveryReady;
    private readonly Func<bool> _dependenciesAlive;
    public bool RecoveryReady { get => _recoveryReady && _dependenciesAlive(); set => _recoveryReady = value; }
    public EffectsState State { get { lock (_stateLock) return _state; } }
    public JournaledEffectsCoordinator(AgentStaWorker worker, EffectsState? initial = null, Func<bool>? dependenciesAlive = null, Func<DisplayInventorySnapshot>? readDisplays = null)
    { _worker = worker; _state = initial ?? new(); _dependenciesAlive = dependenciesAlive ?? (() => true); _readDisplays = readDisplays; }
    public void AttachBackend(IColorBackend backend)
    {
        lock (_stateLock)
        {
            if (_backend is not null || _state.Enabled) throw new InvalidOperationException("Backend can only be attached before effects are enabled.");
            _backend = backend;
        }
    }
    public bool CanWrite(WriteFence fence)
    {
        lock (_stateLock) return RecoveryReady && !_state.Capabilities.AdvancedColorActive
            && EffectsReducer.CanWrite(_state, fence.Revision, fence.Generation);
    }
    public async Task<EffectsAcknowledgment> SendAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default)
    {
        Reduction reduction;
        lock (_stateLock)
        {
            if (envelope.Command is ApplyProfile && (!RecoveryReady || !_state.Capabilities.Supports(BackendKind.Magnification)))
            {
                var reason = !RecoveryReady ? "Independent recovery is not ready; desktop effects remain off." :
                    _state.Capabilities.Backends.FirstOrDefault(b => b.Backend == BackendKind.Magnification)?.Reason ?? "Global color backend is unavailable.";
                return new(new(CompletionStatus.Blocked, _state.Revision, _state.Generation, reason), _state,
                    new(false, "Agent", "SafetyGate", reason, _state.Revision));
            }
            reduction = EffectsReducer.Reduce(_state, envelope);
            if (reduction.Result.Status == CompletionStatus.Accepted && (envelope.Command is PreviewMode or ConfirmMode or CancelPreview))
                return new(new(CompletionStatus.NotImplemented, _state.Revision, _state.Generation,
                    "Mode preview/rollback is not wired; desired state was left unchanged."), _state, ApplyResult.NotImplemented("DisplayTransaction", _state.Revision));
            _state = reduction.State;
        }
        if (reduction.Result.Status != CompletionStatus.Accepted)
            return new(reduction.Result, State, new(false, "Coordinator", reduction.Result.Status.ToString(), reduction.Result.Explanation, State.Revision));
        var requested = reduction.State;
        if (envelope.Command is QueryDisplays query)
        {
            return await _worker.InvokeAsync(() =>
            {
                if (_readDisplays is null) return new EffectsAcknowledgment(reduction.Result, State, ApplyResult.NotImplemented("DisplayInventory", State.Revision));
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var epoch = Volatile.Read(ref _displayEpoch);
                    if (query.InventoryId is null) { _displaySnapshot = _readDisplays(); _snapshotEpoch = epoch; }
                    if (_displaySnapshot is null || _snapshotEpoch != Volatile.Read(ref _displayEpoch) || query.InventoryId is { } id && id != _displaySnapshot.Id)
                        throw new ArgumentException("Inventory expired; refresh before requesting another page.");
                    var page = _displaySnapshot.Page(query.Display, query.Offset);
                    return new EffectsAcknowledgment(reduction.Result, State,
                        new(true, "DisplayInventory", null, page.Explanation, State.Revision), page);
                }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    if (query.InventoryId is null) _displaySnapshot = null;
                    return new EffectsAcknowledgment(reduction.Result, State, new(false, "DisplayInventory", "InventoryUnavailable", error.Message, State.Revision));
                }
            });
        }
        if (envelope.Command is GetState)
        {
            // A heartbeat must pass through the actual STA, rather than hiding a hung setter.
            await _worker.InvokeAsync(() => true, safetyPriority: true);
            return new(reduction.Result, State, new(true, "Agent", null, "Authoritative state; STA responsive.", requested.Revision));
        }
        var priority = envelope.Command is Restore or Shutdown or Suspend or SetHold { Held: false } or Enable { Enabled: false };
        var completion = await _worker.InvokeAsync(async () =>
        {
            if (_backend is null) return new ApplyResult(false, "Agent", "SafetyGate", "No owned backend is configured.", requested.Revision);
            if (envelope.Command is PreviewMode or ConfirmMode or CancelPreview)
                return ApplyResult.NotImplemented("DisplayTransaction", requested.Revision);
            try
            {
                if (!requested.Effective.Enabled) return (await _backend.RestoreAsync(CancellationToken.None)) with { Revision = requested.Revision };
                if (!CanWrite(new(requested.Revision, requested.Generation)))
                    return new ApplyResult(false, "Agent", "StaleRevisionOrSafetyGate", "Superseded or recovery is not ready.", requested.Revision);
                var applied = await _backend.ApplyAsync(requested.Effective.GlobalColor, requested.Revision, cancellationToken);
                if (!applied.Success && State.Revision == requested.Revision && State.Generation == requested.Generation)
                {
                    var restored = await _backend.RestoreAsync(CancellationToken.None);
                    if (!restored.Success) RequireRecovery();
                    return applied with { Explanation = $"{applied.Explanation} Rollback: {restored.Explanation}" };
                }
                return applied;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            { RequireRecovery(); return new ApplyResult(false, "Agent", "BackendFailure", error.Message, requested.Revision); }
        }, priority);
        lock (_stateLock) _state = EffectsReducer.Observe(_state, completion, requested.Generation);
        CompanionLaunch.Log("Agent", $"{envelope.Command.GetType().Name} rev={requested.Revision} gen={requested.Generation} saturation={requested.Effective.GlobalColor.SaturationPercent:0.##} success={completion.Success} code={completion.ErrorCode ?? "none"}: {completion.Explanation}");
        return new(reduction.Result, State, completion);
    }
    public void UpdateCapabilities(CapabilityReport report)
    {
        lock (_stateLock) _state = _state with { Capabilities = report };
    }
    public void UpdateAutomationStatus(string status)
    { lock (_stateLock) _state = _state with { AutomationStatus = status.Length <= 4096 ? status : status[..4096] }; }
    private void RequireRecovery()
    {
        RecoveryReady = false;
        lock (_stateLock) _state = _state with { RecoveryPending = true, Held = false, Enabled = false,
            Revision = checked(_state.Revision + 1), Generation = checked(_state.Generation + 1) };
    }
}
