using System.Collections.Immutable;
using System.Windows.Input;
using System.Windows.Media;
using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Chromatic.Core.Validation;
using Chromatic.Core.Abstractions;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Ipc;

namespace Chromatic.App.ViewModels;
public sealed record PreviewSwatch(string Label, Brush Original, Brush Adjusted);
public sealed class DisplayViewModel : PageViewModel
{
    private ColorSettings _draft = new();
    private readonly IEffectsClient? _client;
    private readonly AppSessionSettings? _session;
    private double _outputWarmth;
    private DisplayIdentity? _selectedOutput;
    private bool _isApplying;
    private ImmutableArray<DisplayIdentity> _outputs = [];
    private string _applyStatus = "Effects off. Sliders change the local sample preview until you press Apply.";
    public double Saturation { get => _draft.SaturationPercent; set => Set(_draft with { SaturationPercent = value }, nameof(Saturation)); }
    public double Brightness { get => _draft.Brightness; set => Set(_draft with { Brightness = value }, nameof(Brightness)); }
    public double Contrast { get => _draft.Contrast; set => Set(_draft with { Contrast = value }, nameof(Contrast)); }
    public double Warmth { get => _draft.Warmth; set => Set(_draft with { Warmth = value }, nameof(Warmth)); }
    public double Gamma { get => _draft.Gamma; set => Set(_draft with { Gamma = value }, nameof(Gamma)); }
    public double Shadows { get => _draft.Shadows; set => Set(_draft with { Shadows = value }, nameof(Shadows)); }
    public double Highlights { get => _draft.Highlights; set => Set(_draft with { Highlights = value }, nameof(Highlights)); }
    public double Vibrance { get => _draft.Vibrance; set { Set(_draft with { Vibrance = value }, nameof(Vibrance)); Changed(nameof(VibranceScale)); } }
    public double VibranceScale { get => (Vibrance + 1) * 50; set => Vibrance = value / 50 - 1; }
    public string CvdMode { get => _draft.CvdMode; set => Set(_draft with { CvdMode = value }, nameof(CvdMode)); }
    public double CvdStrength { get => _draft.CvdStrength; set => Set(_draft with { CvdStrength = value }, nameof(CvdStrength)); }
    public double HueRotation { get => _draft.HueRotationDegrees; set => Set(_draft with { HueRotationDegrees = value }, nameof(HueRotation)); }
    public double DarkenHue { get => _draft.DarkenHueDegrees; set => Set(_draft with { DarkenHueDegrees = value }, nameof(DarkenHue)); }
    public double DarkenPercent { get => _draft.DarkenStrength * 100; set => Set(_draft with { DarkenStrength = value / 100 }, nameof(DarkenPercent)); }
    public string AccentStatus => DarkenPercent == 0 && HueRotation == 0 ? "Colour darkening and hue rotation are off." :
        "Experimental GLOBAL colour transform. Matching colours everywhere may darken; opposite colours may brighten/clip. Neutral white stays white. Black holo/loot behavior in Rust has not been visually qualified.";
    public string[] CvdModes { get; } = ["None", "RedWeak", "GreenWeak", "BlueWeak"];
    public ImmutableArray<DisplayIdentity> Outputs => _outputs;
    public DisplayIdentity? SelectedOutput
    {
        get => _selectedOutput;
        set { if (_selectedOutput == value) return; _selectedOutput = value; Changed(); RefreshAvailability(); }
    }
    public double OutputWarmth
    {
        get => _outputWarmth;
        set { SettingsValidator.Range(value, -1, 1, nameof(OutputWarmth)); _outputWarmth = value; Changed(); RebuildPreview(); }
    }
    public string ApplyStatus { get => _applyStatus; private set { _applyStatus = value; Changed(); } }
    public ImmutableArray<PreviewSwatch> Swatches { get; private set; } = [];
    public ColorSettings DraftColor => _draft;
    public void LoadColorDraft(ColorSettings color)
    {
        SettingsValidator.ValidateColor(color);
        _draft = color;
        foreach (var property in new[] { nameof(Saturation), nameof(Brightness), nameof(Contrast), nameof(Warmth), nameof(Gamma),
            nameof(Shadows), nameof(Highlights), nameof(Vibrance), nameof(VibranceScale), nameof(CvdMode), nameof(CvdStrength), nameof(OutputWarmth), nameof(HueRotation), nameof(DarkenHue), nameof(DarkenPercent), nameof(AccentStatus) }) Changed(property);
        RebuildPreview();
        ApplyStatus = "Preset loaded into the local sample preview. Press Apply to request desktop changes.";
    }
    public ICommand ResetCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand ResetTonesCommand { get; }
    public ICommand OpenDisplaySettingsCommand { get; }
    public ICommand PrepareNvidiaCommand { get; }
    public ICommand PrepareBlueHoloCommand { get; }
    public ICommand PrepareBlueLootCommand { get; }
    public ICommand ResetAccentCommand { get; }
    public string FullscreenStatus => _client?.State.Capabilities.Backends.Any(b => b.Backend == BackendKind.NvidiaDvc && b.Status == Capability.Experimental) == true
        ? "Experimental NVIDIA vibrance is opted in. Select one output, keep Saturation at 100%, adjust Vibrance and Apply. Check for Magnification+NvidiaDvc in the result; a software fallback does not cover exclusive fullscreen. Driver/game behavior needs your visual check."
        : "Exclusive fullscreen can bypass desktop saturation. For NVIDIA vibrance: Settings → opt in to experimental NVIDIA DVC → Save → restart, then select one output and use Prepare NVIDIA vibrance. This uses the display driver, without game hooks. Fullscreen support is unqualified.";
    public bool IsApplying { get => _isApplying; private set { _isApplying = value; Changed(); RefreshAvailability(); } }
    public bool CanApply => _client is not null && (_client is not CompanionSession session || session.Connected)
        && !_client.State.RecoveryPending && _client.State.Capabilities.Supports(BackendKind.Magnification);
    public override string CapabilityStatus => CanApply ? "Agent ready for explicit Apply · hardware unqualified" : "Agent/SDR unavailable · review backend diagnostics";
    public string ConnectionStatus => _client switch
    {
        CompanionSession { Failure: { } failure } => failure,
        CompanionSession { Connected: false } => "Agent is not connected. Start with Run-Chromatic.cmd; connection errors are also shown here.",
        null => "Agent is not connected. The sample preview remains available.",
        _ when _client.State.RecoveryPending => "Recovery is required before display effects can be enabled.",
        _ when !_client.State.Capabilities.Supports(BackendKind.Magnification) => _client.State.Capabilities.Backends
            .FirstOrDefault(b => b.Backend == BackendKind.Magnification)?.Reason ?? "Global color is unavailable.",
        _ => "Agent connected. Sliders edit the preview; Apply sends them to the desktop. Restore returns your captured original colors."
    };
    public bool CanUseGamma => !_isApplying && SelectedOutput is not null && _client is not null &&
        _client.State.Capabilities.Supports(BackendKind.SdrGamma, SelectedOutput) &&
        _client.State.Capabilities.Backends.Count(b => b.Backend == BackendKind.SdrGamma) == 1;
    public string GammaStatus => CanUseGamma ? "SDR tonal controls target only the selected output." :
        "Gamma/shadows/highlights require gamma compatibility enabled in Settings, Save + restart, and one selected active SDR output. Multiple-output LUTs remain unqualified.";
    public DisplayViewModel(IEffectsClient? client = null, AppSessionSettings? settings = null) : base("Display", "Your desktop, in balance.",
        "The matrix affects every display. Gamma is an opt-in SDR compatibility route on the explicitly selected output. CVD is an experimental approximation. Native DVC requires its separate opt-in and a single output; extra saturation uses software. Capture and driver support remain unqualified.")
    {
        _client = client; _session = settings; _draft = settings?.Current.GlobalColor ?? new();
        ApplyCommand = new AsyncCommand(ApplyAsync, error => ApplyStatus = $"Apply failed: {error.Message}");
        PrepareBlueHoloCommand = new RelayCommand(_ =>
        {
            LoadColorDraft(new(150) { HueRotationDegrees = 15, DarkenHueDegrees = 255, DarkenStrength = 4 });
            ApplyStatus = "BLUE holo test draft only. In Rust, select a blue Holo Sight Colour yourself under Accessibility. This global transform is original and unverified in Rust, not an exact PlexusX preset. Preview, then explicitly Apply. Other blue/purple objects change too.";
        });
        PrepareBlueLootCommand = new RelayCommand(_ =>
        {
            LoadColorDraft(new(120) { DarkenHueDegrees = 240, DarkenStrength = 4 });
            ApplyStatus = "Blue-tinted inventory-accent TEST draft only. It may darken a coloured highlight; a neutral white highlight stays white. No game/hover detection or confirmed black-loot result. Preview and Apply only if you accept the whole-image changes.";
        });
        ResetAccentCommand = new RelayCommand(_ =>
        {
            HueRotation = 0; DarkenPercent = 0; DarkenHue = 0;
            ApplyStatus = "Colour darkening/hue rotation reset in the draft. Other base controls preserved; press Apply when ready.";
        });
        PrepareNvidiaCommand = new RelayCommand(_ =>
        {
            _outputWarmth = 0; LoadColorDraft(new(Vibrance: .5));
            if (Outputs.Length == 1) SelectedOutput = Outputs[0];
            RebuildPreview();
            ApplyStatus = "Draft prepared: Saturation 100% (neutral), Vibrance 75/100, other controls neutral. Nothing applied. Enable NVIDIA DVC in Settings, Save + restart if needed, then select one output and Apply. The result must name NvidiaDvc for native vibrance.";
        });
        ResetTonesCommand = new RelayCommand(_ =>
        { Gamma = 1; Shadows = 0; Highlights = 0; OutputWarmth = 0; ApplyStatus = "Optional tones reset. Global saturation and affine controls are preserved; press Apply."; });
        OpenDisplaySettingsCommand = new RelayCommand(_ =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:display") { UseShellExecute = true });
                ApplyStatus = "Windows Display settings opened. Turn off HDR/automatic color management yourself to use SDR controls, then return and press Apply.";
            }
            catch (Exception error) { ApplyStatus = $"Could not open Windows Display settings: {error.Message}"; }
        });
        ResetCommand = new RelayCommand(_ =>
        {
            _outputWarmth = 0; LoadColorDraft(new());
        });
        RebuildPreview();
        Refresh();
    }
    public void Refresh()
    {
        var next = _client?.State.Capabilities.Backends.Where(b => b.Display is not null)
            .Select(b => b.Display!).Distinct().ToImmutableArray() ?? [];
        if (!_outputs.SequenceEqual(next))
        {
            var previous = _selectedOutput; _outputs = next;
            _selectedOutput = previous is null ? null : next.SingleOrDefault(d =>
                d.MonitorDevicePath.Equals(previous.MonitorDevicePath, StringComparison.OrdinalIgnoreCase) && d.EdidSerial == previous.EdidSerial);
            Changed(nameof(Outputs)); Changed(nameof(SelectedOutput));
        }
        RefreshAvailability();
    }
    private void RefreshAvailability()
    {
        Changed(nameof(CanApply)); Changed(nameof(CapabilityStatus)); Changed(nameof(ConnectionStatus));
        Changed(nameof(CanUseGamma)); Changed(nameof(GammaStatus));
        Changed(nameof(FullscreenStatus));
    }
    public async Task ApplyAsync()
    {
        if (IsApplying) return;
        if (!CanApply || _client is null) { ApplyStatus = $"Cannot apply: {ConnectionStatus}"; return; }
        if ((_draft.Gamma != 1 || _draft.Shadows != 0 || _draft.Highlights != 0 || _outputWarmth != 0) && !CanUseGamma)
        { ApplyStatus = "Cannot apply optional tones: " + GammaStatus + " Use Reset optional tones for saturation-only changes."; return; }
        var profile = new DisplayProfile { GlobalColor = _draft,
            Monitors = SelectedOutput is null ? [] : [new(SelectedOutput, Warmth: _outputWarmth)] };
        IsApplying = true;
        ApplyStatus = $"Applying {profile.GlobalColor.SaturationPercent:0}% saturation / vibrance {(profile.GlobalColor.Vibrance + 1) * 50:0}/100… waiting for Agent readback.";
        try
        {
            var state = _client.State;
            var ack = await _client.SendAsync(new(Guid.NewGuid(), state.Revision, state.Generation, new ApplyProfile(profile)));
            var superseded = ack.State.Revision != ack.Decision.Revision || ack.State.Generation != ack.Decision.Generation;
            var success = ack.Decision.Status == CompletionStatus.Accepted && ack.Completion.Success && ack.State.Effective.Enabled
                && !superseded && ack.Completion.Revision == ack.Decision.Revision;
            ApplyStatus = success ? $"Applied {profile.GlobalColor.SaturationPercent:0}% saturation · vibrance {(profile.GlobalColor.Vibrance + 1) * 50:0}/100 · {ack.Completion.Backend}. {ack.Completion.Explanation}" :
                superseded ? "Not applied: another command superseded this Apply. Review the current state and press Apply again." :
                $"Not applied · {ack.Completion.ErrorCode ?? ack.Decision.Status.ToString()}: {ack.Completion.Explanation ?? ack.Decision.Explanation}";
            if (success && _session is not null)
                _session.Current = _session.Current with { GlobalColor = profile.GlobalColor, Monitors = profile.Monitors, EffectsEnabled = false };
        }
        catch (Exception error) { ApplyStatus = $"Not applied: {error.Message}. Check Agent connection or restart using Run-Chromatic.cmd."; }
        finally { IsApplying = false; Refresh(); }
    }
    private void Set(ColorSettings value, string property)
    {
        SettingsValidator.ValidateColor(value); _draft = value; Changed(property); Changed(nameof(AccentStatus)); RebuildPreview();
    }
    private void RebuildPreview()
    {
        var matrix = ColorMatrixBuilder.Build(_draft);
        (string Label, Rgba Value)[] samples = [("Warm", new(.75, .3, .2)), ("Leaf", new(.25, .6, .2)),
            ("Sky", new(.2, .45, .8)), ("Dark", new(.12, .15, .18)), ("Gray", new(.5, .5, .5)), ("Light", new(.9, .9, .9)),
            ("Red sight", new(1, 0, 0)), ("Green sight", new(0, 1, 0)), ("Blue sight", new(0, 0, 1)),
            ("Blue tint", new(.4, .5, .95)), ("White", new(1, 1, 1)), ("Black", new(0, 0, 0))];
        Rgba Tone(Rgba value) => new(
            ToneCurveBuilder.Map(Math.Clamp(value.R, 0, 1), _draft.Gamma, _draft.Shadows, _draft.Highlights) * (1 + .1 * _outputWarmth),
            ToneCurveBuilder.Map(Math.Clamp(value.G, 0, 1), _draft.Gamma, _draft.Shadows, _draft.Highlights),
            ToneCurveBuilder.Map(Math.Clamp(value.B, 0, 1), _draft.Gamma, _draft.Shadows, _draft.Highlights) * (1 - .1 * _outputWarmth), value.A);
        Swatches = samples.Select(s => new PreviewSwatch(s.Label, BrushFor(s.Value), BrushFor(Tone(matrix.Transform(s.Value))))).ToImmutableArray();
        Changed(nameof(Swatches));
    }
    private static Brush BrushFor(Rgba value)
    {
        static byte Channel(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(Channel(value.R), Channel(value.G), Channel(value.B)));
        brush.Freeze(); return brush;
    }
}
