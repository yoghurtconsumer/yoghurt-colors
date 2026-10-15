using System.Windows.Input;
using Chromatic.Core.Models;
namespace Chromatic.App.ViewModels;
public sealed class CustomizeViewModel : PageViewModel
{
    public ICommand DefaultThemeCommand { get; }
    public ICommand SlateThemeCommand { get; }
    public CustomizeViewModel(AppSessionSettings session) : base("Customize", "Make room for your own style.",
        "Local backgrounds and Windows appearance integrations are scheduled for Stage 5.")
    {
        void Set(ThemeKind theme) { session.Current = session.Current with { Theme = theme }; App.ApplyTheme(theme); }
        DefaultThemeCommand = new RelayCommand(_ => Set(ThemeKind.Default));
        SlateThemeCommand = new RelayCommand(_ => Set(ThemeKind.Slate));
    }
}
