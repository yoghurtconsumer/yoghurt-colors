using Chromatic.Core.Models;

namespace Chromatic.Core.Validation;

public enum RemoteAction { SetColor, ActivateScene, GetState }
public sealed record RemoteCommand(int SchemaVersion, long Seq, long BaseRevision, RemoteAction Action,
    ColorSettings? Color = null, string? PresetId = null);
public static class RemoteCommandValidator
{
    public static void Validate(RemoteCommand command, long lastSequence, IReadOnlySet<string> allowedScenes)
    {
        if (command.SchemaVersion != 1 || command.Seq <= lastSequence || command.Seq < 1 || command.BaseRevision < 0)
            throw new ArgumentException("Unknown version or stale remote sequence/revision.");
        switch (command.Action)
        {
            case RemoteAction.SetColor when command.Color is not null && command.PresetId is null:
                SettingsValidator.ValidateColor(command.Color); break;
            case RemoteAction.ActivateScene when command.Color is null && command.PresetId is not null && allowedScenes.Contains(command.PresetId): break;
            case RemoteAction.GetState when command.Color is null && command.PresetId is null: break;
            default: throw new ArgumentException("Remote color-only whitelist rejected the command.");
        }
    }
}
