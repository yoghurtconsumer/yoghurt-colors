using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Infrastructure.Ipc;
using Chromatic.Infrastructure.Persistence;
using Chromatic.App.ViewModels;
using Chromatic.Media;
using Chromatic.Overlay;
using Chromatic.Remote;
using Microsoft.Extensions.DependencyInjection;

namespace Chromatic.App;
public sealed class AppSessionSettings(AppSettings settings)
{
    public AppSettings Current { get; set; } = settings;
}
public static class CompositionRoot
{
    public static ServiceProvider Build(AppSettings settings, SettingsRepository repository, IEffectsClient? effects = null, string? dataDirectory = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AppSessionSettings(settings)); services.AddSingleton(repository);
        dataDirectory ??= System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChromaticDesktop");
        services.AddSingleton(new LibraryRepository(System.IO.Path.Combine(dataDirectory, "library.json")));
        services.AddSingleton(new GameLibraryRepository(System.IO.Path.Combine(dataDirectory, "games.json")));
        services.AddSingleton(new AutomationRepository(System.IO.Path.Combine(dataDirectory, "automation.json")));
        services.AddSingleton(new PresetProtocolRegistration(Environment.ProcessPath!, new UserProtocolRegistry()));
        services.AddSingleton<IEffectsClient>(effects ?? new EffectsPipeClient());
        services.AddSingleton<IMediaController, GsmtcController>();
        services.AddSingleton<OverlayHost>(); services.AddSingleton<PhoneServer>();
        services.AddSingleton<DisplayViewModel>(); services.AddSingleton<PresetsViewModel>();
        services.AddSingleton<AutomationViewModel>();
        services.AddSingleton<GamesViewModel>(); services.AddSingleton<ResolutionViewModel>();
        services.AddSingleton<CrosshairViewModel>(); services.AddSingleton<MonitorViewModel>();
        services.AddSingleton<CustomizeViewModel>(); services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MusicViewModel>(); services.AddSingleton<PhoneViewModel>();
        services.AddSingleton<MainViewModel>(); services.AddSingleton<TrayService>(); services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
