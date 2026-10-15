using Chromatic.Core.Models;

namespace Chromatic.Core.Validation;

public static class RecoveryValidator
{
    public static void Validate(RecoverySnapshot snapshot)
    {
        if (snapshot.SchemaVersion != 1 || snapshot.JournalId == Guid.Empty || snapshot.AgentPid <= 0 ||
            snapshot.AgentStartTicks <= 0 || snapshot.Generation < 0 || snapshot.SessionId < 0 || !Enum.IsDefined(snapshot.Status))
            throw new ArgumentException("Invalid recovery identity/version.");
        SettingsValidator.Text(snapshot.UserSid, 184, "user SID");
        if (!snapshot.UserSid.StartsWith("S-1-", StringComparison.Ordinal)) throw new ArgumentException("Invalid user SID.");
        if (snapshot.Resources.IsDefault || snapshot.Resources.Length > 128) throw new ArgumentException("Invalid resource count.");
        var ids = new HashSet<Guid>();
        var descriptors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in snapshot.Resources)
        {
            if (resource.OwnershipId == Guid.Empty || !ids.Add(resource.OwnershipId) || resource.Generation < 0 ||
                !Enum.IsDefined(resource.Kind) || !Enum.IsDefined(resource.Scope)) throw new ArgumentException("Invalid ownership.");
            if (!descriptors.Add($"{resource.Kind}:{resource.Display?.MonitorDevicePath}")) throw new ArgumentException("Duplicate resource identity.");
            if (resource.PersistentUndo && resource.Kind != OwnedResourceKind.TemporaryDdc) throw new ArgumentException("Color resources are temporary, not persistent Undo resources.");
            if (resource.Kind == OwnedResourceKind.GlobalMatrix || resource.Kind == OwnedResourceKind.DisplayTopology)
            {
                if (resource.Scope != EffectScope.GlobalDesktop || resource.Display is not null)
                    throw new ArgumentException("Global resource cannot carry a per-monitor scope.");
            }
            else
            {
                if (resource.Scope != EffectScope.PerMonitor || resource.Display is null)
                    throw new ArgumentException("Per-monitor resource requires a display identity.");
                SettingsValidator.ValidateDisplay(resource.Display);
            }
            ValidateValue(resource.Kind, resource.Baseline); ValidateValue(resource.Kind, resource.LastApplied);
            if (resource.PendingValue is not null) ValidateValue(resource.Kind, resource.PendingValue);
        }
        if (snapshot.Preview is not null)
        {
            var p = snapshot.Preview;
            if (p.Id == Guid.Empty || !Enum.IsDefined(p.Status) || p.Generation < 0) throw new ArgumentException("Invalid preview.");
            SettingsValidator.ValidateDisplay(p.Display); SettingsValidator.ValidateIntent(p.Intent);
        }
    }
    private static void ValidateValue(OwnedResourceKind kind, ResourceValue value)
    {
        if (value.Values.IsDefault || value.Values.Any(v => !double.IsFinite(v))) throw new ArgumentException("Invalid baseline numbers.");
        var expected = kind switch { OwnedResourceKind.GlobalMatrix => 25, OwnedResourceKind.GammaRamp => 768,
            OwnedResourceKind.DisplayTopology => 0, _ => 1 };
        if (value.Values.Length != expected) throw new ArgumentException("Wrong baseline size.");
        if (kind == OwnedResourceKind.GlobalMatrix && value.Values.Any(v => Math.Abs(v) > float.MaxValue))
            throw new ArgumentException("Matrix baseline is outside native float range.");
        if (kind is OwnedResourceKind.NativeVibrance or OwnedResourceKind.TemporaryDdc &&
            value.Values.Any(v => v != Math.Truncate(v) || v < (kind == OwnedResourceKind.NativeVibrance ? int.MinValue : 0) || v > (kind == OwnedResourceKind.NativeVibrance ? int.MaxValue : uint.MaxValue)))
            throw new ArgumentException("Invalid native integer baseline.");
        if (kind == OwnedResourceKind.GammaRamp && value.Values.Any(v => v < 0 || v > 65535 || v != Math.Truncate(v)))
            throw new ArgumentException("Gamma baseline must preserve exact WORDs.");
        if (kind == OwnedResourceKind.Hdr && value.Values[0] is not (0 or 1)) throw new ArgumentException("Invalid HDR baseline.");
        if (kind == OwnedResourceKind.DisplayTopology)
        {
            if (value.TopologyFormat != "ccd-v1-x64" || value.TopologyBase64 is null || value.TopologyBase64.Length > 524288 ||
                Convert.FromBase64String(value.TopologyBase64).Length == 0) throw new ArgumentException("Invalid topology payload.");
        }
        else if (value.TopologyBase64 is not null || value.TopologyFormat is not null)
            throw new ArgumentException("Topology data on a non-topology resource.");
    }
}
