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
    public string[] CvdModes { get; } = ["None", "RedWeak", "GreenWeak", "BlueWeak"];
    public ImmutableArray<DisplayIdentity> Outputs => _client?.State.Capabilities.Backends.Where(b => b.Display is not null)
        .Select(b => b.Display!).Distinct().ToImmutableArray() ?? [];
    public DisplayIdentity? SelectedOutput { get; set; }
    public double OutputWarmth
    {
        get => _outputWarmth;
        set { SettingsValidator.Range(value, -1, 1, nameof(OutputWarmth)); _outputWarmth = value; Changed(); RebuildPreview(); }
    }
    public string ApplyStatus { get => _applyStatus; private set { _applyStatus = value; Changed(); } }
    public ImmutableArray<PreviewSwatch> Swatches { get; private set; } = [];
    public ICommand ResetCommand { get; }
    public ICommand ApplyCommand { get; }
    public bool CanApply => _client is not null && (_client is not CompanionSession session || session.Connected)
        && !_client.State.RecoveryPending && _client.State.Capabilities.Supports(BackendKind.Magnification);
    public override string CapabilityStatus => CanApply ? "Agent ready for explicit Apply · hardware unqualified" : "Agent/SDR unavailable · review backend diagnostics";
    public DisplayViewModel(IEffectsClient? client = null, AppSessionSettings? settings = null) : base("Display", "Your desktop, in balance.",
        "The matrix affects every display. Gamma is an opt-in SDR compatibility route on the explicitly selected output. CVD is an experimental approximation. Native DVC requires its separate opt-in and a single output; extra saturation uses software. Capture and driver support remain unqualified.")
    {
        _client = client; _session = settings; _draft = settings?.Current.GlobalColor ?? new();
        ApplyCommand = new AsyncCommand(ApplyAsync, error => ApplyStatus = $"Apply failed: {error.Message}");
        ResetCommand = new RelayCommand(_ =>
        {
            _draft = new(); _outputWarmth = 0; Changed(nameof(OutputWarmth)); RebuildPreview();
            Changed(nameof(Saturation)); Changed(nameof(Brightness)); Changed(nameof(Contrast)); Changed(nameof(Warmth));
            Changed(nameof(Gamma)); Changed(nameof(Shadows)); Changed(nameof(Highlights)); Changed(nameof(Vibrance)); Changed(nameof(CvdMode)); Changed(nameof(CvdStrength));
            Changed(nameof(VibranceScale));
        });
        RebuildPreview();
    }
    public void Refresh() { Changed(nameof(CanApply)); Changed(nameof(Outputs)); Changed(nameof(CapabilityStatus)); }
    public async Task ApplyAsync()
    {
        if (!CanApply || _client is null) { ApplyStatus = "Agent or SDR capability unavailable. Review diagnostics."; return; }
        async Task<bool> Send(EffectsCommand command)
        {
            var state = _client.State;
            var ack = await _client.SendAsync(new(Guid.NewGuid(), state.Revision, state.Generation, command));
            ApplyStatus = $"{ack.Completion.ErrorCode ?? "Completed"}: {ack.Completion.Explanation}";
            return ack.Decision.Status == CompletionStatus.Accepted && ack.Completion.Success;
        }
        if (!await Send(new Enable(false))) return;
        var profile = new DisplayProfile { GlobalColor = _draft,
            Monitors = SelectedOutput is null ? [] : [new(SelectedOutput, Warmth: _outputWarmth)] };
        if (!await Send(new ActivatePreset(profile))) return;
        foreach (var reason in _client.State.Suspensions)
            if (!await Send(new Resume(reason))) return;
        if (await Send(new Enable(true)) && _session is not null)
            _session.Current = _session.Current with { GlobalColor = profile.GlobalColor, Monitors = profile.Monitors, EffectsEnabled = false };
        Refresh();
    }
    private void Set(ColorSettings value, string property)
    {
        SettingsValidator.ValidateColor(value); _draft = value; Changed(property); RebuildPreview();
    }
    private void RebuildPreview()
    {
        var matrix = ColorMatrixBuilder.Build(_draft);
        (string Label, Rgba Value)[] samples = [("Warm", new(.75, .3, .2)), ("Leaf", new(.25, .6, .2)),
            ("Sky", new(.2, .45, .8)), ("Dark", new(.12, .15, .18)), ("Gray", new(.5, .5, .5)), ("Light", new(.9, .9, .9))];
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
