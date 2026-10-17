using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;

namespace Chromatic.Agent;

// One consumer owns input state, debounce and discrete command dispatch. Native
// callbacks never execute reducer, disk or display work. Tests inject all input.
public sealed class AutomationRuntime : IAsyncDisposable
{
    private readonly IEffectsClient _client;
    private readonly Action<string> _status;
    private readonly Func<AutomationSettings, IAutomationInput> _factory;
    private readonly Func<long> _clock;
    private readonly int _uiPid;
    private AutomationSettings? _installed;
    private IAutomationInput? _input;
    private HoldTracker? _hold;
    private bool _eligible;
    private nint _pendingWindow, _activeWindow;
    private bool _pending, _knownForeground;
    private ExecutableIdentity? _identity;
    private string? _profileId;
    private long _due, _nextObserve, _nextReconcile;
    private string _lastAction = "";
    private readonly List<(Task<EffectsAcknowledgment> Task, Action<EffectsAcknowledgment>? Completed)> _commands = [];
    public AutomationRuntime(IEffectsClient client, int uiPid, Action<string> status,
        Func<AutomationSettings, IAutomationInput>? factory = null, Func<long>? clock = null)
    { _client = client; _uiPid = uiPid; _status = status; _factory = factory ?? (s => new NativeAutomationInput(s)); _clock = clock ?? (() => Environment.TickCount64); }
    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await TickAsync();
            await Task.Delay(25, token);
        }
    }
    public async Task TickAsync()
    {
        foreach (var pending in _commands.Where(p => p.Task.IsCompleted).ToArray())
        { _commands.Remove(pending); Complete(pending.Task, pending.Completed); }
        var settings = _client.State.Automation; var now = _clock();
        if (!ReferenceEquals(_installed, settings))
        {
            await ClearHoldAsync(); _input?.Dispose(); _input = null; _installed = settings;
            _eligible = false; _knownForeground = false; _pending = false; _lastAction = "";
            _hold = new(settings.HoldKeys, settings.AcceptInjectedInput);
            if (!settings.NeedsListener) { _status("Input and automatic profiles are off."); return; }
            try
            {
                _input = _factory(settings); _hold.RequireFreshPress(_input.IsDown);
                _nextObserve = now; _nextReconcile = now + 100; _status(_input.Status);
            }
            catch (Exception e) { _status($"Listeners unavailable: {e.Message}. Save configuration again to retry."); return; }
        }
        if (_input is null || _hold is null) return;
        if (_input.Overflowed || _commands.Count >= 16)
        {
            await ClearHoldAsync(); _input.Dispose(); _input = null;
            if (settings.AutomaticProfiles) Dispatch(new ForegroundChanged(null, null));
            _status("Input queue overflow; listeners disabled and hold cleared. Save configuration again to restart."); return;
        }
        var state = _client.State;
        if (!state.Effective.Enabled || state.PreviewActive || state.Held != _hold.Held) await ClearHoldAsync();
        for (var count = 0; count < 64 && _commands.Count < 16 && _input.TryRead(out var signal); count++)
        {
            if (signal.Kind == AutomationSignalKind.Foreground)
            {
                await ClearHoldAsync(); _eligible = false; _pendingWindow = signal.Window; _due = now + 150; _pending = true;
            }
            else if (signal.Kind == AutomationSignalKind.Hotkey && settings.HotkeysEnabled)
            {
                var binding = settings.Hotkeys.FirstOrDefault(h => h.Id == signal.HotkeyId);
                if (binding is not null) await HotkeyAsync(binding, settings);
            }
            else if (signal.Kind == AutomationSignalKind.Key && settings.Boost.Enabled)
            {
                state = _client.State;
                var confirmed = _hold.Held || state.Observed.LastResult is { Success: true } && state.Observed.Revision == state.Revision && state.Observed.Generation == state.Generation;
                var eligible = _eligible && state.Effective.Enabled && BasicLiftAvailable(state) && confirmed;
                if (_hold.Process(signal.Input, eligible) is { } held)
                {
                    Dispatch(new SetHold(held), ack =>
                    {
                        if (!ack.Completion.Success && held && _client.State.Held && _client.State.Generation == ack.Decision.Generation)
                        { _hold?.RequireFreshPress(k => _input?.IsDown(k) == true); Dispatch(new SetHold(false)); }
                    });
                }
            }
        }
        if (settings.AutomaticProfiles || settings.Boost.Enabled)
        {
            if (now >= _nextObserve)
            {
                var observed = _input.ObserveForeground(); _nextObserve = now + 500;
                if ((!_pending || observed.Window != _pendingWindow) && (!_knownForeground || observed.Window != _activeWindow || EligibleIdentity(observed.Identity) != _identity))
                { await ClearHoldAsync(); _eligible = false; _pendingWindow = observed.Window; _pending = true; _due = now + 150; }
            }
            if (_pending && now >= _due)
            {
                var observed = _input.ObserveForeground();
                if (observed.Window != _pendingWindow)
                { await ClearHoldAsync(); _pendingWindow = observed.Window; _due = now + 150; }
                else if (!_client.State.PreviewActive && !_client.State.ShuttingDown && !_client.State.RecoveryPending && _client.State.Suspensions.IsEmpty)
                {
                    var identity = EligibleIdentity(observed.Identity);
                    var rule = identity is null ? null : ProfileResolver.Resolve(identity, settings.Rules);
                    var profile = rule is null ? null : settings.Profiles.FirstOrDefault(p => p.Id == rule);
                    // Explicit per-output intentions never follow a window by guessing.
                    if (profile is { Monitors.Length: > 0 } && !profile.Monitors.Any(m => string.Equals(m.Display.GdiName, observed.MonitorGdi, StringComparison.OrdinalIgnoreCase))) profile = null;
                    var changed = !_knownForeground || _identity != identity || _profileId != profile?.Id || _activeWindow != observed.Window;
                    _eligible = profile is not null;
                    _identity = identity; _activeWindow = observed.Window; _profileId = profile?.Id; _knownForeground = true; _pending = false;
                    if (settings.AutomaticProfiles && changed)
                    {
                        Dispatch(new ForegroundChanged(identity, profile), ack =>
                        { if (ack.Decision.Status != CompletionStatus.Accepted) { _pending = true; _due = _clock() + 150; _knownForeground = false; } });
                    }
                    _status(_input.Status + " " + (_eligible ? $"Matched {profile!.Name}. Global colors affect every display." : "Desktop fallback / no eligible game.") + " " + _lastAction);
                }
            }
        }
        if (now >= _nextReconcile)
        {
            _nextReconcile = now + 100;
            if (_hold.Reconcile(_input.IsDown)) Dispatch(new SetHold(false));
        }
    }
    private ExecutableIdentity? EligibleIdentity(ExecutableIdentity? who) => who is not null && who.ProcessId != _uiPid && who.ProcessId != Environment.ProcessId ? who : null;
    private Task HotkeyAsync(HotkeyBinding key, AutomationSettings settings)
    {
        EffectsCommand? command = key.Action switch
        {
            HotkeyAction.Preset when settings.Profiles.FirstOrDefault(p => p.Id == key.ProfileId) is { } profile => new ActivatePreset(profile),
            HotkeyAction.Pause => _client.State.Suspensions.Contains(SuspensionReason.Pause) ? new Resume(SuspensionReason.Pause) : new Suspend(SuspensionReason.Pause),
            HotkeyAction.Restore => new Restore(), HotkeyAction.Screenshot => new TakeScreenshot(), _ => null
        };
        if (command is not null) Dispatch(command, ack =>
        {
            _lastAction = $"Hotkey {key.Id}: {ack.Completion.ErrorCode ?? "completed"} {ack.Completion.Explanation}";
            _status((_input?.Status ?? "") + " " + _lastAction);
        });
        return Task.CompletedTask;
    }
    public static bool BasicLiftAvailable(EffectsState state) => state.Effective.GlobalColor is { Gamma: 1, Shadows: 0, Highlights: 0 } &&
        state.Effective.Monitors.All(m => m.Gamma == 1 && m.Warmth == 0) &&
        !(state.Capabilities.Backends.Any(b => b.Backend == BackendKind.NvidiaDvc && b.Status == Capability.Experimental) &&
          state.Effective.GlobalColor is { SaturationPercent: 100 } color && color.Vibrance != 0 && state.Effective.Monitors.Length == 1);
    private Task ClearHoldAsync()
    {
        _hold?.Reset(); if (_client.State.Held) Dispatch(new SetHold(false));
        return Task.CompletedTask;
    }
    // SendAsync reduces synchronously before its first native/disk await. Keep
    // consuming input while completion is pending so release immediately fences
    // queued writes, rather than waiting behind the boost setter or journal.
    private void Dispatch(EffectsCommand command, Action<EffectsAcknowledgment>? completed = null)
    {
        var task = SendAsync(command);
        if (task.IsCompleted) Complete(task, completed); else _commands.Add((task, completed));
    }
    private void Complete(Task<EffectsAcknowledgment> task, Action<EffectsAcknowledgment>? completed)
    {
        try { var ack = task.GetAwaiter().GetResult(); completed?.Invoke(ack); }
        catch (Exception e)
        { _hold?.Reset(); throw new InvalidOperationException("Input dispatch failed; Agent must shut down and restore.", e); }
    }
    private Task<EffectsAcknowledgment> SendAsync(EffectsCommand command)
    { var state = _client.State; return _client.SendAsync(new(Guid.NewGuid(), state.Revision, state.Generation, command)); }
    public async ValueTask DisposeAsync()
    {
        await ClearHoldAsync(); _input?.Dispose(); _input = null;
        try { await Task.WhenAll(_commands.Select(p => p.Task)); } catch (Exception e) { _status("Input shutdown: " + e.Message); }
        _commands.Clear();
    }
}
