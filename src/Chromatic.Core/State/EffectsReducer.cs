using Chromatic.Core.Models;
using Chromatic.Core.Validation;

namespace Chromatic.Core.State;

public sealed record Reduction(EffectsState State, CommandResult Result);

public static class EffectsReducer
{
    public static Reduction Reduce(EffectsState state, CommandEnvelope envelope)
    {
        var command = envelope.Command;
        if (command is null) return new(state, new(CompletionStatus.Invalid, state.Revision, state.Generation, "Command is required."));
        // Safety commands fence even previously queued work. Held=true can never bypass revisions.
        var priority = command is Restore or Shutdown or Suspend or SetHold { Held: false } or Enable { Enabled: false };
        Reduction Reject(CompletionStatus status, string reason) => new(state,
            new(status, state.Revision, state.Generation, reason));
        if (envelope.BaseRevision < 0 || envelope.Generation < 0 || envelope.CommandId == Guid.Empty)
            return Reject(CompletionStatus.Invalid, "Invalid command envelope.");
        if (!priority && (envelope.BaseRevision != state.Revision || envelope.Generation != state.Generation))
            return Reject(CompletionStatus.StaleRevision, "Rebase on the authoritative revision and generation.");
        if (state.ShuttingDown && command is not Shutdown and not Restore)
            return Reject(CompletionStatus.Blocked, "Shutdown has priority.");
        if (state.RecoveryPending && command is not Restore and not Shutdown and not Suspend and not SetHold { Held: false })
            return Reject(CompletionStatus.Blocked, "Recovery must complete before accepting effects.");
        if (command is Suspend suspend && !Enum.IsDefined(suspend.Reason) || command is Resume resume && !Enum.IsDefined(resume.Reason))
            return Reject(CompletionStatus.Invalid, "Unknown suspension reason.");
        EffectsState next;
        try
        {
            next = command switch
            {
                Enable c => state with { Enabled = c.Enabled, Held = false },
                SetColor c => SetBaseColor(state, c.Color),
                ActivatePreset c => Activate(state, c.Profile),
                ForegroundChanged c => ChangeForeground(state, c),
                SetHold c when !c.Held => state with { Held = false },
                SetHold when !state.Effective.Enabled || !state.HoldBoost.Enabled =>
                    throw new InvalidOperationException("Hold requires enabled, unsuspended effects and opt-in boost."),
                SetHold => state with { Held = true },
                PreviewMode c => StartPreview(state, c),
                ConfirmMode c => EndPreview(state, c.TransactionId, PreviewStatus.Confirmed),
                CancelPreview c => EndPreview(state, c.TransactionId, PreviewStatus.RollingBack),
                Suspend c => state with { Suspensions = state.Suspensions.Add(c.Reason), Held = false,
                    Preview = CancelActivePreview(state) },
                Resume c => state with { Suspensions = state.Suspensions.Remove(c.Reason), Held = false },
                Restore => state with { Enabled = false, Held = false, ManualProfile = null,
                    Preview = CancelActivePreview(state) },
                Shutdown => state with { Enabled = false, Held = false, ShuttingDown = true,
                    Preview = CancelActivePreview(state) },
                _ => throw new ArgumentException("Unknown typed command.")
            };
        }
        catch (ArgumentException e) { return Reject(CompletionStatus.Invalid, e.Message); }
        catch (InvalidOperationException e) { return Reject(CompletionStatus.Blocked, e.Message); }
        next = next with { Revision = checked(state.Revision + 1), Generation = checked(state.Generation + 1) };
        return new(next, new(CompletionStatus.Accepted, next.Revision, next.Generation,
            "Desired state accepted; backend acknowledgment is required for completion."));
    }

    public static bool CanWrite(EffectsState current, long revision, long generation) =>
        current.Revision == revision && current.Generation == generation && current.Effective.Enabled;

    public static EffectsState Observe(EffectsState state, ApplyResult result, long generation)
    {
        if (result.Revision != state.Revision || generation != state.Generation) return state;
        return state with { Observed = new(result.Revision, generation, result) };
    }

    private static EffectsState SetBaseColor(EffectsState state, ColorSettings color)
    {
        SettingsValidator.ValidateColor(color);
        return state with { ManualProfile = state.BaseProfile with { GlobalColor = color } };
    }
    private static EffectsState Activate(EffectsState state, DisplayProfile profile)
    {
        SettingsValidator.ValidateProfile(profile);
        return state with { ManualProfile = profile };
    }
    private static EffectsState ChangeForeground(EffectsState state, ForegroundChanged command)
    {
        if (command.Profile is not null) SettingsValidator.ValidateProfile(command.Profile);
        if (command.Identity is not null)
        {
            SettingsValidator.Text(command.Identity.NormalizedPath, 32768, "executable path");
            if (command.Identity.ProcessId <= 0 || command.Identity.ProcessStartTicks <= 0)
                throw new ArgumentException("Foreground identity requires PID and process start time.");
        }
        return state with { Foreground = command.Identity, GameProfile = command.Profile,
            ManualProfile = null, Held = false };
    }
    private static PreviewTransaction? CancelActivePreview(EffectsState state) =>
        state.PreviewActive ? state.Preview! with { Status = PreviewStatus.RollingBack } : state.Preview;
    private static EffectsState StartPreview(EffectsState state, PreviewMode command)
    {
        SettingsValidator.ValidateIntent(command.Intent);
        SettingsValidator.ValidateDisplay(command.Display);
        if (state.PreviewActive || !state.Suspensions.IsEmpty || command.TransactionId == Guid.Empty)
            throw new InvalidOperationException("Preview is blocked by another preview or suspension.");
        // Clock/deadline validation belongs to the coordinator, not this deterministic reducer.
        return state with { Held = false, Preview = new(command.TransactionId, PreviewStatus.Prepared,
            command.Display, command.Intent, command.DeadlineUtc, checked(state.Generation + 1)) };
    }
    private static EffectsState EndPreview(EffectsState state, Guid id, PreviewStatus target)
    {
        if (state.Preview?.Id != id || !state.PreviewActive)
            throw new InvalidOperationException("No matching active preview.");
        if (target == PreviewStatus.Confirmed && state.Preview.Status != PreviewStatus.AwaitingConfirmation)
            throw new InvalidOperationException("Only a completed native apply can be confirmed.");
        return state with { Held = false, Preview = state.Preview with { Status = target } };
    }
}
