using System.Collections.Immutable;
using System.Windows.Input;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;

namespace Chromatic.App.ViewModels;
public sealed class ResolutionViewModel : PageViewModel
{
    private readonly IEffectsClient? _client;
    private Guid _inventoryId;
    private string _status = "Refresh to read active monitors and supported modes. No display changes are made.";
    private string _plan = "Review an enumerated source size before starting an experimental 15-second preview. Physical refresh changes and HDR toggles remain unavailable.";
    private DisplayOutputInfo? _selected;
    private DisplayModeChoice? _selectedMode;
    private ScalingIntent _scaling;
    public override string CapabilityStatus => "Display inventory / experimental SDR preview · Stage 4 in progress";
    public ImmutableArray<DisplayOutputInfo> Outputs { get; private set; } = [];
    public ImmutableArray<DisplayModeChoice> Modes { get; private set; } = [];
    public DisplayOutputInfo? SelectedOutput
    {
        get => _selected;
        set { _selected = value; Modes = []; SelectedMode = null; Changed(); Changed(nameof(Modes)); Changed(nameof(OutputStatus)); }
    }
    public DisplayModeChoice? SelectedMode { get => _selectedMode; set { _selectedMode = value; Plan = "Draft selection changed; review again. No display changes made."; Changed(); } }
    public ScalingIntent Scaling { get => _scaling; set { _scaling = value; Plan = "Draft scaling changed; review again. No display changes made."; Changed(); } }
    public ScalingIntent[] ScalingOptions { get; } = Enum.GetValues<ScalingIntent>();
    public string Status { get => _status; private set { _status = value; Changed(); } }
    public string Plan { get => _plan; private set { _plan = value; Changed(); } }
    public string OutputStatus => SelectedOutput is { } o ?
        $"{o.Display.GdiName} · source {o.SourceId}, target {o.Display.TargetId} · position ({o.X}, {o.Y})\n" +
        $"Current: {o.Current.Width} × {o.Current.Height} · {(double)o.Current.RefreshRate.Numerator / o.Current.RefreshRate.Denominator:0.###} Hz ({o.Current.RefreshRate.Numerator}/{o.Current.RefreshRate.Denominator}) · {o.ColorMode}\n" +
        $"{o.HdrStatus}\n" + (o.Cloned ? "Shared-source clone: mode planning is blocked." : "Independent source mapping.") : "Select a monitor.";
    public bool IsPreviewActive => _client?.State.PreviewActive == true;
    public bool CanKeep => _client?.State.Preview?.Status == PreviewStatus.AwaitingConfirmation;
    public bool CanPreview => _client?.State.Capabilities.Backends.Any(b => b.Backend == BackendKind.DisplayMode && b.Status == Capability.Experimental) == true && !IsPreviewActive;
    public string PreviewStatusText => _client?.State.Preview is { } p ?
        $"Display preview: {p.Status} · {Math.Max(0, (p.DeadlineUtc - DateTimeOffset.UtcNow).TotalSeconds):0}s left. Agent owns the deadline. Colours stay off; Keep does not save to Windows' database." :
        "Opt in to experimental SDR display previews in Settings, Save and restart. Source size/scaling at the current physical timing only.";
    public ICommand PreviewCommand { get; }
    public ICommand KeepCommand { get; }
    public ICommand RevertCommand { get; }
    public void RefreshStatus()
    {
        Changed(nameof(IsPreviewActive)); Changed(nameof(CanKeep)); Changed(nameof(CanPreview)); Changed(nameof(PreviewStatusText));
    }
    public async Task PreviewAsync()
    {
        if (_client is null || SelectedOutput is null || SelectedMode is null || !CanPreview) throw new InvalidOperationException("Select a mode and opt in to experimental display previews first.");
        if (SelectedOutput.Cloned) throw new InvalidOperationException("Cloned shared sources are not supported.");
        var state = _client.State;
        var ack = await _client.SendAsync(new(Guid.NewGuid(), state.Revision, state.Generation,
            new PreviewMode(Guid.NewGuid(), SelectedOutput.Display, new(SelectedMode.Mode.Width, SelectedMode.Mode.Height, SelectedMode.Mode.RefreshRate, Scaling), DateTimeOffset.UtcNow.AddSeconds(15))));
        Status = $"{ack.Completion.ErrorCode ?? "Completed"}: {ack.Completion.Explanation}"; RefreshStatus();
    }
    public async Task KeepAsync()
    {
        if (_client is null || !CanKeep || _client.State.Preview is not { } preview) throw new InvalidOperationException("No completed active preview to keep.");
        var state = _client.State;
        var ack = await _client.SendAsync(new(Guid.NewGuid(), state.Revision, state.Generation, new ConfirmMode(preview.Id)));
        Status = $"{ack.Completion.ErrorCode ?? "Completed"}: {ack.Completion.Explanation}"; RefreshStatus();
    }
    public async Task RevertAsync()
    {
        if (_client is null || !IsPreviewActive || _client.State.Preview is not { } preview) return;
        var state = _client.State;
        var ack = await _client.SendAsync(new(Guid.NewGuid(), state.Revision, state.Generation, new CancelPreview(preview.Id)));
        Status = $"{ack.Completion.ErrorCode ?? "Completed"}: {ack.Completion.Explanation}"; RefreshStatus();
        if (!ack.Completion.Success) throw new InvalidOperationException(Status);
    }
    public ICommand RefreshCommand { get; }
    public ICommand LoadModesCommand { get; }
    public ICommand ReviewPlanCommand { get; }
    public ResolutionViewModel(IEffectsClient? client = null) : base("Resolution", "Inspect supported modes before changing a display.",
        "Read-only mode inventory and local draft plans are implemented. Experimental SDR source-size/scaling previews have independent Agent deadline and Recovery. Physical refresh changes, HDR toggles, legacy CDS and custom timing remain unavailable.")
    {
        _client = client;
        PreviewCommand = new AsyncCommand(PreviewAsync, e => Status = "Preview failed: " + e.Message);
        KeepCommand = new AsyncCommand(KeepAsync, e => Status = "Keep failed: " + e.Message);
        RevertCommand = new AsyncCommand(RevertAsync, e => Status = "Revert failed: " + e.Message);
        RefreshCommand = new AsyncCommand(RefreshAsync, e => Status = "Inventory failed: " + e.Message);
        LoadModesCommand = new AsyncCommand(LoadModesAsync, e => Status = "Modes failed: " + e.Message);
        ReviewPlanCommand = new RelayCommand(_ =>
        {
            try
            {
                if (SelectedOutput is null || SelectedMode is null) throw new InvalidOperationException("Select a monitor and load/select its mode first.");
                var snapshot = new DisplayInventorySnapshot(_inventoryId, Outputs,
                    ImmutableDictionary<string, ImmutableArray<DisplayModeChoice>>.Empty.Add(SelectedOutput.Display.MonitorDevicePath, Modes), "Local draft");
                var plan = DisplayModePlanner.Draft(snapshot, _inventoryId, SelectedOutput.Display, SelectedMode.Id, Scaling);
                Plan = $"Requested source: {plan.Intent.Width} × {plan.Intent.Height} · {plan.Intent.RefreshRate.Numerator}/{plan.Intent.RefreshRate.Denominator} Hz · scaling {Scaling}. " +
                    $"{plan.PreservedOutputs.Length} other active target(s) preserved in this draft.\n{plan.Explanation}";
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException) { Plan = "Plan unavailable: " + e.Message; }
        });
    }
    private async Task<DisplayInventoryPage> QueryAsync(QueryDisplays command)
    {
        if (_client is null) throw new InvalidOperationException("Authenticated Agent is unavailable.");
        var state = _client.State; var ack = await _client.SendAsync(new(Guid.NewGuid(), state.Revision, state.Generation, command));
        if (!ack.Completion.Success || ack.Inventory is null) throw new InvalidOperationException(ack.Completion.Explanation ?? "No inventory returned.");
        if (command.InventoryId is { } id && id != ack.Inventory.InventoryId) throw new InvalidOperationException("Inventory changed; refresh first.");
        return ack.Inventory;
    }
    public async Task RefreshAsync()
    {
        var path = SelectedOutput?.Display.MonitorDevicePath;
        Outputs = []; SelectedOutput = null; _inventoryId = Guid.Empty; Changed(nameof(Outputs));
        Plan = "No display changes. Load modes, review the draft, then explicitly Preview if enabled.";
        var page = await QueryAsync(new()); var id = page.InventoryId; var outputs = ImmutableArray.CreateBuilder<DisplayOutputInfo>();
        for (var count = 0; ; count++)
        {
            if (count >= 32 || page.Outputs.Length > 4 || !page.Modes.IsEmpty) throw new InvalidOperationException("Invalid inventory pagination.");
            outputs.AddRange(page.Outputs);
            if (page.NextOffset is not { } offset) break;
            if (offset != outputs.Count) throw new InvalidOperationException("Invalid inventory offset.");
            page = await QueryAsync(new(id, Offset: offset));
        }
        Outputs = outputs.ToImmutable(); _inventoryId = id; Changed(nameof(Outputs));
        SelectedOutput = Outputs.FirstOrDefault(o => o.Display.MonitorDevicePath.Equals(path, StringComparison.OrdinalIgnoreCase)) ?? Outputs.FirstOrDefault();
        Status = $"{Outputs.Length} active target(s). {page.Explanation}";
    }
    public async Task LoadModesAsync()
    {
        var output = SelectedOutput ?? throw new InvalidOperationException("Select a monitor first."); var id = _inventoryId;
        Modes = []; SelectedMode = null; Changed(nameof(Modes));
        var modes = ImmutableArray.CreateBuilder<DisplayModeChoice>(); var offset = 0;
        for (var count = 0; ; count++)
        {
            if (count >= 129) throw new InvalidOperationException("Mode pagination exceeds its bound.");
            var page = await QueryAsync(new(id, output.Display, offset));
            if (page.Outputs.Length != 1 || page.Outputs[0].Display != output.Display || page.Modes.Length > 16) throw new InvalidOperationException("Mode identity mismatch.");
            modes.AddRange(page.Modes);
            if (page.NextOffset is not { } next) break;
            if (next <= offset || next != modes.Count) throw new InvalidOperationException("Invalid mode page offset."); offset = next;
        }
        if (_inventoryId != id || SelectedOutput != output) { Status = "Selection changed; modes discarded."; return; }
        Modes = modes.ToImmutable(); Changed(nameof(Modes)); SelectedMode = Modes.FirstOrDefault(m => m.IsCurrent) ?? Modes.FirstOrDefault();
        Status = $"{Modes.Length} progressive 32-bit mode choice(s). Current CCD rational refresh remains separate from integer driver enumeration. For preview, choose a source-size option at current target timing. Physical refresh changes remain unavailable.";
    }
}
