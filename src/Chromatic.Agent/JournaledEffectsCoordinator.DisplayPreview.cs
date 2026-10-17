using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Effects;
using Chromatic.Infrastructure.Ipc;

namespace Chromatic.Agent;
public sealed partial class JournaledEffectsCoordinator
{
    private readonly TimeProvider _clock;
    private IDisplayModeResource? _modeResource;
    private OwnedResourceController? _modeOwned;
    private CancellationTokenSource? _previewTimer;
    private long _previewStart;
    private int _previewOperations;
    public void AttachDisplayPreview(IDisplayModeResource resource, OwnedResourceController owned)
    {
        lock (_stateLock)
        {
            if (_modeResource is not null || _state.PreviewActive) throw new InvalidOperationException("Preview resource already attached.");
            _modeResource = resource; _modeOwned = owned;
        }
    }
    public IWriteSafetyGate ModeSafetyGate => new ModeGate(this);
    private sealed class ModeGate(JournaledEffectsCoordinator owner) : IWriteSafetyGate
    {
        public bool RecoveryReady => owner.RecoveryReady;
        public bool CanWrite(WriteFence fence)
        {
            var state = owner.State;
            return RecoveryReady && !owner.PreviewExpired() && !state.ShuttingDown && !state.RecoveryPending && state.Suspensions.IsEmpty &&
                state.Revision == fence.Revision && state.Generation == fence.Generation && state.Preview?.Status == PreviewStatus.Applying;
        }
    }
    private EffectsAcknowledgment PreviewUnavailable() => new(new(CompletionStatus.NotImplemented, _state.Revision, _state.Generation,
        "Opt in to experimental display previews, Save and restart. Independent recovery must be ready."), _state,
        ApplyResult.NotImplemented("DisplayPreview", _state.Revision));
    private bool PreviewExpired() => State.PreviewActive && _clock.GetElapsedTime(_previewStart, _clock.GetTimestamp()) >= TimeSpan.FromSeconds(15);
    private void StartPreviewDeadline(PreviewTransaction preview)
    {
        _previewTimer?.Cancel(); _previewTimer?.Dispose(); _previewTimer = new();
        _previewStart = _clock.GetTimestamp(); var token = _previewTimer.Token;
        _ = WaitForDeadlineAsync();
        async Task WaitForDeadlineAsync()
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), _clock, token).ConfigureAwait(false);
                var state = State;
                if (state.PreviewActive && state.Preview?.Id == preview.Id)
                    await SendAsync(new(Guid.NewGuid(), state.Revision, state.Generation, new CancelPreview(preview.Id)));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception error) { RequireRecovery(); CompanionLaunch.Log("Agent", "Preview deadline failed: " + error.Message); }
        }
    }
    private void PreviewObserved(Guid id, PreviewStatus status, ApplyResult result)
    {
        lock (_stateLock)
        {
            if (_state.Preview?.Id != id) return;
            _state = _state with { Preview = _state.Preview with { Status = status }, Enabled = false, Held = false,
                Observed = new(_state.Revision, _state.Generation, result with { Revision = _state.Revision }) };
        }
    }
    private async Task<ApplyResult> BeginPreviewAsync(EffectsState requested, CancellationToken token)
    {
        var preview = requested.Preview!; var revision = requested.Revision;
        try
        {
            // Remove SDR effects before any topology write. The user's base draft
            // stays saved, but colours require explicit Apply again afterwards.
            if (_backend is null) throw new InvalidOperationException("Color restore backend is unavailable.");
            var colors = await _backend.RestoreAsync(CancellationToken.None);
            if (!colors.Success) { RequireRecovery(); throw new InvalidOperationException("Color baseline restore failed: " + colors.Explanation); }
            if (State.Generation != requested.Generation || !State.PreviewActive || PreviewExpired()) throw new WriteFenceRejectedException();
            PreviewObserved(preview.Id, PreviewStatus.Applying, new(false, "DisplayPreview", "Applying", "Preparing journaled display preview.", revision));
            preview = preview with { Status = PreviewStatus.Applying };
            await _modeOwned!.SetPreviewAsync(preview, token);
            var result = await _modeOwned.ApplyComputedAsync(_modeResource!, baseline => _modeResource!.Plan(baseline, preview.Display, preview.Intent),
                new(revision, requested.Generation), token);
            if (!result.Success) throw new InvalidOperationException(result.ErrorCode + ": " + result.Explanation);
            if (State.Generation != requested.Generation || PreviewExpired()) throw new WriteFenceRejectedException();
            await _modeOwned.SetPreviewAsync(preview with { Status = PreviewStatus.AwaitingConfirmation }, CancellationToken.None);
            if (State.Generation != requested.Generation || PreviewExpired()) throw new WriteFenceRejectedException();
            result = new(true, "DisplayPreview", null, "Preview read back. Keep locally within the remaining 15-second window or Revert. Physical refresh timing is preserved. Colours remain off.", revision);
            PreviewObserved(preview.Id, PreviewStatus.AwaitingConfirmation, result); InvalidateDisplayInventory(); return result;
        }
        catch (Exception error)
        {
            var reverted = await RollbackPreviewAsync(revision, preview.Id);
            var failed = new ApplyResult(false, "DisplayPreview", "PreviewFailed", error.Message + " " + reverted.Explanation, revision);
            if (!reverted.Success) RequireRecovery();
            return failed;
        }
    }
    private async Task<ApplyResult> KeepPreviewAsync(EffectsState requested)
    {
        var preview = requested.Preview!;
        try
        {
            if (_modeOwned is null || _modeResource is null || !RecoveryReady || State.Preview?.Id != preview.Id || State.Preview.Status != PreviewStatus.Confirmed)
                throw new InvalidOperationException("Preview was interrupted before Keep.");
            var current = await _modeResource.ReadAsync(CancellationToken.None);
            var resource = _modeOwned.Snapshot.Resources.Single();
            if (!_modeResource.Equivalent(current, resource.LastApplied)) throw new InvalidOperationException("Topology changed outside this preview; Keep refused.");
            await _modeOwned.KeepPreviewAsync(preview, CancellationToken.None);
            _previewTimer?.Cancel(); InvalidateDisplayInventory();
            var result = new ApplyResult(true, "DisplayPreview", null, "Kept the confirmed desktop mode. No Windows database/custom timing was saved. Colours remain off until Apply.", requested.Revision);
            PreviewObserved(preview.Id, PreviewStatus.Confirmed, result); return result;
        }
        catch (Exception error)
        {
            var reverted = await RollbackPreviewAsync(requested.Revision, preview.Id);
            return new(false, "DisplayPreview", "KeepFailed", error.Message + " " + reverted.Explanation, requested.Revision);
        }
    }
    private async Task<ApplyResult> RollbackPreviewAsync(long revision, Guid? expectedId = null)
    {
        if (expectedId is { } expected && State.Preview?.Id != expected)
            return new(true, "DisplayPreview", null, "Superseded preview; no display write.", revision);
        _previewTimer?.Cancel();
        if (_modeOwned is null || _modeResource is null) return ApplyResult.NotImplemented("DisplayPreview", revision);
        if (_modeOwned.Snapshot.Status == JournalStatus.Restored && _modeOwned.Snapshot.Resources.IsEmpty && _modeOwned.Snapshot.Preview?.Status == PreviewStatus.Confirmed)
        {
            var noWrite = new ApplyResult(true, "DisplayPreview", null, "No temporary topology is owned; confirmed mode preserved.", revision);
            if (State.Preview is { } current && current.Id != _modeOwned.Snapshot.Preview.Id)
                PreviewObserved(current.Id, PreviewStatus.Reverted, noWrite);
            return noWrite;
        }
        var id = State.Preview?.Id ?? _modeOwned.Snapshot.Preview?.Id;
        var result = await _modeOwned.RestoreAsync([_modeResource], revision, CancellationToken.None);
        if (id is { } active)
        {
            var status = result.Success ? PreviewStatus.Reverted : PreviewStatus.Failed;
            if (_modeOwned.Snapshot.Preview is { } saved)
                try
                {
                    if (result.Success) await _modeOwned.KeepPreviewAsync(saved with { Status = status }, CancellationToken.None, keep: false);
                    else await _modeOwned.SetPreviewAsync(saved with { Status = status }, CancellationToken.None);
                }
                catch (Exception error) { result = new(false, "DisplayPreview", "JournalFailure", error.Message, revision); status = PreviewStatus.Failed; }
            PreviewObserved(active, status, result);
        }
        if (!result.Success) RequireRecovery(); InvalidateDisplayInventory(); return result;
    }
    public async Task<bool> CheckPreviewDisplayChangeAsync()
    {
        if (!State.PreviewActive || _modeResource is null || _modeOwned is null) return false;
        var result = await _worker.InvokeAsync(async () =>
        {
            if (!State.PreviewActive) return true;
            try
            {
                var actual = await _modeResource.ReadAsync(CancellationToken.None);
                var resource = _modeOwned.Snapshot.Resources.SingleOrDefault();
                if (resource is null || _modeResource.Equivalent(actual, resource.LastApplied) || resource.PendingValue is not null && _modeResource.Equivalent(actual, resource.PendingValue)) return true;
            }
            catch (Exception) { }
            return false;
        }, true);
        if (!result && State.Preview is { } preview)
            await SendAsync(new(Guid.NewGuid(), State.Revision, State.Generation, new CancelPreview(preview.Id)));
        return true;
    }
}
