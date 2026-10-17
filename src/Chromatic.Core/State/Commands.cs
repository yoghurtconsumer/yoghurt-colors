using System.Text.Json.Serialization;
using Chromatic.Core.Models;

namespace Chromatic.Core.State;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Enable), "enable")]
[JsonDerivedType(typeof(SetColor), "setColor")]
[JsonDerivedType(typeof(ActivatePreset), "activatePreset")]
[JsonDerivedType(typeof(ForegroundChanged), "foregroundChanged")]
[JsonDerivedType(typeof(SetHold), "setHold")]
[JsonDerivedType(typeof(PreviewMode), "previewMode")]
[JsonDerivedType(typeof(ConfirmMode), "confirmMode")]
[JsonDerivedType(typeof(CancelPreview), "cancelPreview")]
[JsonDerivedType(typeof(Suspend), "suspend")]
[JsonDerivedType(typeof(Resume), "resume")]
[JsonDerivedType(typeof(Restore), "restore")]
[JsonDerivedType(typeof(Shutdown), "shutdown")]
[JsonDerivedType(typeof(GetState), "getState")]
[JsonDerivedType(typeof(QueryDisplays), "queryDisplays")]
[JsonDerivedType(typeof(ApplyProfile), "applyProfile")]
[JsonDerivedType(typeof(ConfigureAutomation), "configureAutomation")]
[JsonDerivedType(typeof(TakeScreenshot), "takeScreenshot")]
public abstract record EffectsCommand;
public sealed record Enable(bool Enabled) : EffectsCommand;
public sealed record SetColor(ColorSettings Color) : EffectsCommand;
public sealed record ActivatePreset(DisplayProfile Profile) : EffectsCommand;
public sealed record ForegroundChanged(ExecutableIdentity? Identity, DisplayProfile? Profile) : EffectsCommand;
public sealed record SetHold(bool Held) : EffectsCommand;
public sealed record PreviewMode(Guid TransactionId, DisplayIdentity Display, DisplayIntent Intent,
    DateTimeOffset DeadlineUtc) : EffectsCommand;
public sealed record ConfirmMode(Guid TransactionId) : EffectsCommand;
public sealed record CancelPreview(Guid TransactionId) : EffectsCommand;
public sealed record Suspend(SuspensionReason Reason) : EffectsCommand;
public sealed record Resume(SuspensionReason Reason) : EffectsCommand;
public sealed record Restore : EffectsCommand;
public sealed record Shutdown : EffectsCommand;
public sealed record GetState : EffectsCommand;
public sealed record ApplyProfile(DisplayProfile Profile) : EffectsCommand;
public sealed record ConfigureAutomation(AutomationSettings Settings) : EffectsCommand;
public sealed record TakeScreenshot : EffectsCommand;
public sealed record CommandEnvelope(Guid CommandId, long BaseRevision, long Generation, EffectsCommand Command);

public sealed record QueryDisplays(Guid? InventoryId = null, DisplayIdentity? Display = null, int Offset = 0) : EffectsCommand;
