using System.Windows;
using System.IO;
using Chromatic.App;
using Chromatic.App.ViewModels;
using Chromatic.Core.Models;
using Chromatic.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Chromatic.Ui.Tests;

public sealed class ShellSmokeTests
{
    [Fact] public void DisplayPreviewDesaturatesSamplesAndResetRestoresDraft()
    {
        var viewModel = new DisplayViewModel();
        viewModel.Saturation = 0;
        foreach (var swatch in viewModel.Swatches)
        {
            var color = Assert.IsType<System.Windows.Media.SolidColorBrush>(swatch.Adjusted).Color;
            Assert.Equal(color.R, color.G); Assert.Equal(color.G, color.B);
        }
        viewModel.Brightness = .1; viewModel.Contrast = 1.5; viewModel.Warmth = .5;
        viewModel.ResetCommand.Execute(null);
        Assert.Equal(100, viewModel.Saturation); Assert.Equal(0, viewModel.Brightness);
        Assert.Equal(1, viewModel.Contrast); Assert.Equal(0, viewModel.Warmth); Assert.False(viewModel.CanApply);
        foreach (var swatch in viewModel.Swatches)
            Assert.Equal(((System.Windows.Media.SolidColorBrush)swatch.Original).Color, ((System.Windows.Media.SolidColorBrush)swatch.Adjusted).Color);
    }
    [Fact] public void DiResolvesEveryNavigationPageAndSafeStatus()
    {
        using var services = CompositionRoot.Build(new(), new SettingsRepository(Path.Combine(Path.GetTempPath(), "Chromatic.Unused.Settings.json")));
        var vm = services.GetRequiredService<MainViewModel>();
        Assert.Equal(new[] { "Display", "Presets / Library", "Games", "Resolution", "Crosshair", "Monitor", "Customize", "Settings" }, vm.Navigation.Select(n => n.Label));
        foreach (var navigation in vm.Navigation)
        {
            vm.Selected = navigation;
            Assert.Same(navigation.Page, vm.CurrentPage);
            if (vm.CurrentPage is DisplayViewModel) Assert.Contains("unavailable", vm.CurrentPage.CapabilityStatus);
            else if (vm.CurrentPage is PresetsViewModel or GamesViewModel) Assert.Contains("Local", vm.CurrentPage.CapabilityStatus);
            else Assert.Equal("NotImplemented", vm.CurrentPage.CapabilityStatus);
        }
        Assert.False(services.GetRequiredService<DisplayViewModel>().CanApply);
        Assert.False(services.GetRequiredService<SettingsViewModel>().CloseToTray);
        Assert.All(vm.Capabilities, capability => Assert.Equal(Capability.Unsupported, capability.Status));
    }
    [Fact] public void XamlAndBothThemesLoadOnStaWithoutShowingWindows()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new Chromatic.App.App(); application.InitializeComponent();
                using var services = CompositionRoot.Build(new(), new SettingsRepository(Path.Combine(Path.GetTempPath(), "Chromatic.Unused.Settings.json")));
                var window = services.GetRequiredService<MainWindow>();
                Assert.NotNull(window.Content); Assert.False(window.IsVisible);
                var vm = services.GetRequiredService<MainViewModel>();
                foreach (var page in vm.Navigation)
                {
                    var template = Assert.IsType<DataTemplate>(window.Resources[new DataTemplateKey(page.Page.GetType())]);
                    var content = Assert.IsAssignableFrom<FrameworkElement>(template.LoadContent());
                    content.DataContext = page.Page;
                    content.Measure(new Size(900, double.PositiveInfinity));
                    content.Arrange(new Rect(new Point(), content.DesiredSize));
                    content.UpdateLayout();
                }
                foreach (var theme in Enum.GetValues<ThemeKind>())
                {
                    Chromatic.App.App.ApplyTheme(theme);
                    foreach (var key in new[] { "CanvasBrush", "SurfaceBrush", "TextBrush", "MutedBrush", "AccentBrush", "BorderBrush", "SelectionBrush" })
                        Assert.True(application.Resources.Contains(key), $"Theme {theme}: missing {key}");
                }
                application.Shutdown();
            }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "STA shell load timed out.");
        Assert.Null(failure);
    }
}
