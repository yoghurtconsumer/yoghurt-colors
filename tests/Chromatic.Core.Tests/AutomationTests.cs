using System.Collections.Immutable;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Core.Validation;
using Xunit;

namespace Chromatic.Core.Tests;

public sealed class AutomationTests
{
    [Fact] public void DefaultsInstallNothingAndConfigurationNeverEnablesEffects()
    {
        var settings = new AutomationSettings(); AutomationValidator.Validate(settings); Assert.False(settings.NeedsListener);
        var result = Send(new(), new ConfigureAutomation(settings with { AutomaticProfiles = true }));
        Assert.Equal(CompletionStatus.Accepted, result.Result.Status); Assert.False(result.State.Enabled); Assert.False(result.State.Held);
    }
    [Fact] public void ChordRequiresAllComponentsIgnoresRepeatAndReleasesAnyComponent()
    {
        var tracker = new HoldTracker([17, 0x75]);
        Assert.Null(tracker.Process(new(162, true), true));
        Assert.True(tracker.Process(new(0x75, true), true));
        Assert.Null(tracker.Process(new(0x75, true), true));
        Assert.False(tracker.Process(new(162, false), true)); Assert.False(tracker.Held);
    }
    [Fact] public void InjectedInputIsIgnoredUnlessExplicitlyAccepted()
    {
        Assert.Null(new HoldTracker([6]).Process(new(6, true, true), true));
        Assert.True(new HoldTracker([6], true).Process(new(6, true, true), true));
    }
    [Fact] public void ReconciliationPollsOnlyConfiguredDownKeysAndCannotActivate()
    {
        var polled = new List<int>(); var tracker = new HoldTracker([17, 6]);
        Assert.False(tracker.Reconcile(k => { polled.Add(k); return true; })); Assert.Empty(polled);
        tracker.Process(new(17, true), true); tracker.Process(new(6, true), true);
        Assert.True(tracker.Reconcile(k => { polled.Add(k); return k != 6; }));
        Assert.All(polled, k => Assert.Contains(k, new[] { 17, 6 })); Assert.False(tracker.Held);
        Assert.Null(tracker.Process(new(6, true), true)); // Ctrl must be released too after reset.
    }
    [Fact] public void ResumeRequiresFreshPressRatherThanAutorepeat()
    {
        var tracker = new HoldTracker([0x75]); tracker.RequireFreshPress(_ => true);
        Assert.Null(tracker.Process(new(0x75, true), true));
        Assert.Null(tracker.Process(new(0x75, false), true));
        Assert.True(tracker.Process(new(0x75, true), true));
        tracker.Reset(); Assert.Null(tracker.Process(new(0x75, true), true));
    }
    [Fact] public void DisabledPressCannotBecomeHoldAfterFocusChange()
    {
        var tracker = new HoldTracker([6]); Assert.Null(tracker.Process(new(6, true), false));
        Assert.Null(tracker.Process(new(6, true), true)); tracker.Process(new(6, false), true);
        Assert.True(tracker.Process(new(6, true), true));
    }
    [Theory] [InlineData("Ctrl+F6")] [InlineData("Mouse5")] [InlineData("Tab")] [InlineData("Ctrl+Alt+Space")]
    public void ChordsRoundTrip(string text) => Assert.Equal(InputChord.Parse(text).ToArray(), InputChord.Parse(InputChord.Format(InputChord.Parse(text))).ToArray());
    [Theory] [InlineData("Ctrl")] [InlineData("F6+F6")] [InlineData("Win+F6")] [InlineData("A+B+C+D+E")]
    public void InvalidChordIsRejected(string text) => Assert.Throws<ArgumentException>(() => InputChord.Parse(text));
    [Fact] public void ConfigRejectsDuplicatePathsReservedHotkeysAndUnapprovedMouseListener()
    {
        var config = new AutomationSettings { Profiles = [new() { Id = "p" }], Rules = [new("1", @"C:\Games\A.exe", "p", true)] };
        AutomationValidator.Validate(config);
        Assert.Throws<ArgumentException>(() => AutomationValidator.Validate(config with { Rules = config.Rules.Add(new("2", @"c:\games\a.EXE", "p")) }));
        Assert.Throws<ArgumentException>(() => AutomationValidator.Validate(config with { Hotkeys = [new(1, 2, 123, HotkeyAction.Restore)] }));
        Assert.Throws<ArgumentException>(() => AutomationValidator.Validate(config with { Boost = new(true), HoldKeys = [6] }));
        AutomationValidator.Validate(config with { Boost = new(true), HoldKeys = [6], MouseHolds = true });
        Assert.Throws<ArgumentException>(() => AutomationValidator.Validate(config with { Rules = [new("1", @"C:\Games\..\A.exe", "p")] }));
    }
    [Fact] public void NewBaseDuringHoldIsRestoredAndStaleConfigurationCannotOverrideRecovery()
    {
        var state = new EffectsState { Enabled = true, Held = true, HoldBoost = new(true) };
        state = Send(state, new ActivatePreset(new() { Id = "new", GlobalColor = new(220) })).State;
        Assert.True(state.Held); state = Send(state, new SetHold(false)).State;
        Assert.Equal(220, state.Effective.GlobalColor.SaturationPercent); Assert.Null(state.Effective.HoldModifier);
        state = state with { RecoveryPending = true };
        Assert.Equal(CompletionStatus.Blocked, Send(state, new ConfigureAutomation(new())).Result.Status);
        Assert.Equal(CompletionStatus.StaleRevision, EffectsReducer.Reduce(state, new(Guid.NewGuid(), 0, 0, new ConfigureAutomation(new()))).Result.Status);
    }
    [Fact] public void ScreenshotDispatchIsHonestAndHasNoStateMutation()
    {
        var state = new EffectsState(); var reduction = Send(state, new TakeScreenshot());
        Assert.Same(state, reduction.State); Assert.Equal(CompletionStatus.NotImplemented, reduction.Result.Status);
    }
    private static Reduction Send(EffectsState state, EffectsCommand command) => EffectsReducer.Reduce(state, new(Guid.NewGuid(), state.Revision, state.Generation, command));
}
