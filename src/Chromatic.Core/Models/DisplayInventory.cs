using System.Collections.Immutable;
using Chromatic.Core.Validation;

namespace Chromatic.Core.Models;

public sealed record DisplayOutputInfo(DisplayIdentity Display, string Name, uint SourceId,
    long SourceAdapterLuid, int X, int Y, DisplayMode Current, uint Rotation, uint Scaling,
    bool Cloned, bool Interlaced, string ColorMode, string HdrStatus);
public sealed record DisplayModeChoice(string Id, DisplayMode Mode, bool IsCurrent, string TimingSource)
{
    public override string ToString() => $"{Mode.Width} × {Mode.Height} · {(double)Mode.RefreshRate.Numerator / Mode.RefreshRate.Denominator:0.###} Hz ({Mode.RefreshRate.Numerator}/{Mode.RefreshRate.Denominator}) · {TimingSource}";
}
// Native snapshot stays inside Agent. Only bounded, typed pages cross IPC.
public sealed record DisplayInventoryPage(Guid InventoryId, ImmutableArray<DisplayOutputInfo> Outputs,
    ImmutableArray<DisplayModeChoice> Modes, int? NextOffset, string Explanation);
public sealed record DisplayInventorySnapshot(Guid Id, ImmutableArray<DisplayOutputInfo> Outputs,
    ImmutableDictionary<string, ImmutableArray<DisplayModeChoice>> Modes, string Explanation)
{
    public DisplayInventoryPage Page(DisplayIdentity? display, int offset)
    {
        SettingsValidator.Range(offset, 0, 2048, nameof(offset));
        if (display is null)
            return new(Id, Outputs.Skip(offset).Take(4).ToImmutableArray(), [], offset + 4 < Outputs.Length ? offset + 4 : null, Explanation);
        var output = Outputs.SingleOrDefault(o => o.Display == display) ?? throw new ArgumentException("Display mapping changed; refresh inventory.");
        var modes = Modes[display.MonitorDevicePath];
        return new(Id, [output], modes.Skip(offset).Take(16).ToImmutableArray(), offset + 16 < modes.Length ? offset + 16 : null, Explanation);
    }
}
public sealed record DisplayModePlan(Guid InventoryId, DisplayOutputInfo Output, DisplayIntent Intent,
    ImmutableArray<DisplayOutputInfo> PreservedOutputs, string Explanation);
public static class DisplayModePlanner
{
    public static DisplayModePlan Draft(DisplayInventorySnapshot inventory, Guid expectedInventory,
        DisplayIdentity display, string modeId, ScalingIntent scaling)
    {
        if (inventory.Id != expectedInventory || expectedInventory == Guid.Empty) throw new ArgumentException("Stale inventory; refresh first.");
        if (!Enum.IsDefined(scaling)) throw new ArgumentException("Unknown scaling intent.");
        var output = inventory.Outputs.SingleOrDefault(o => o.Display == display) ?? throw new ArgumentException("Display mapping changed.");
        if (output.Cloned) throw new InvalidOperationException("Shared-source clone changes require a qualified topology transaction.");
        var mode = inventory.Modes[display.MonitorDevicePath].SingleOrDefault(m => m.Id == modeId) ?? throw new ArgumentException("Only enumerated modes can be selected.");
        var intent = new DisplayIntent(mode.Mode.Width, mode.Mode.Height, mode.Mode.RefreshRate, scaling);
        SettingsValidator.ValidateIntent(intent);
        return new(inventory.Id, output, intent, inventory.Outputs.Where(o => o.Display != display).ToImmutableArray(),
            "Draft only. Complete native topology capture, validate/apply, HDR prerequisites and independent 15-second rollback are required before mutation. " +
            (scaling == ScalingIntent.Stretched ? "Stretch is a driver request; it cannot force game FOV. " : "") +
            "DEVMODE integer refresh is not exact CCD timing; no pixel clock or custom timing is invented.");
    }
}
