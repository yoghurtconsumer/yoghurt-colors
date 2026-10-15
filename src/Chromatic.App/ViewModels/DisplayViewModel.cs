using System.Collections.Immutable;
using System.Windows.Input;
using System.Windows.Media;
using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Chromatic.Core.Validation;

namespace Chromatic.App.ViewModels;
public sealed record PreviewSwatch(string Label, Brush Original, Brush Adjusted);
public sealed class DisplayViewModel : PageViewModel
{
    private ColorSettings _draft = new();
    public double Saturation { get => _draft.SaturationPercent; set => Set(_draft with { SaturationPercent = value }, nameof(Saturation)); }
    public double Brightness { get => _draft.Brightness; set => Set(_draft with { Brightness = value }, nameof(Brightness)); }
    public double Contrast { get => _draft.Contrast; set => Set(_draft with { Contrast = value }, nameof(Contrast)); }
    public double Warmth { get => _draft.Warmth; set => Set(_draft with { Warmth = value }, nameof(Warmth)); }
    public ImmutableArray<PreviewSwatch> Swatches { get; private set; } = [];
    public ICommand ResetCommand { get; }
    public bool CanApply => false;
    public DisplayViewModel() : base("Display", "Your desktop, in balance.",
        "The preview changes only these sample colors. Global display apply remains unavailable until Agent ownership and recovery are implemented.")
    {
        ResetCommand = new RelayCommand(_ =>
        {
            _draft = new(); RebuildPreview();
            Changed(nameof(Saturation)); Changed(nameof(Brightness)); Changed(nameof(Contrast)); Changed(nameof(Warmth));
        });
        RebuildPreview();
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
        Swatches = samples.Select(s => new PreviewSwatch(s.Label, BrushFor(s.Value), BrushFor(matrix.Transform(s.Value)))).ToImmutableArray();
        Changed(nameof(Swatches));
    }
    private static Brush BrushFor(Rgba value)
    {
        static byte Channel(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(Channel(value.R), Channel(value.G), Channel(value.B)));
        brush.Freeze(); return brush;
    }
}
