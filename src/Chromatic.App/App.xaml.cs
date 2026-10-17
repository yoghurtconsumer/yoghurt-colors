using System.IO;
using System.Windows;
using Chromatic.Core.Models;
using Chromatic.Infrastructure.Persistence;
using Chromatic.Infrastructure.Ipc;
using Chromatic.Native;
using Microsoft.Extensions.DependencyInjection;

namespace Chromatic.App;
public partial class App : Application
{
    private ServiceProvider? _services;
    private CompanionSession? _companions;
    private bool _exiting;
    private readonly StartupDiagnostics _startup = new();
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) => _startup.Failed(args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _startup.Failed(args.ExceptionObject as Exception ?? new InvalidOperationException("Unhandled application failure."));
        try
        {
            if (e.Args.Contains("--diagnostics", StringComparer.Ordinal))
            { Console.WriteLine(ReadOnlyDiagnostics.Save()); Shutdown(0); return; }
            await StartAsync(e.Args.Contains("--startup-smoke-test", StringComparer.Ordinal));
        }
        catch (Exception error)
        {
            _startup.Failed(error);
            MessageBox.Show($"Chromatic Desktop could not start.\n\n{error.Message}\n\nDetails: {_startup.ReportPath}",
                "Chromatic Desktop - startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    private async Task StartAsync(bool smokeTest)
    {
        if (!RuntimeCapabilities.MeetsMinimum)
        {
            throw new PlatformNotSupportedException("Chromatic Desktop requires native x64 Windows 10 build 19041 or newer.");
        }
        var repository = new SettingsRepository(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChromaticDesktop", "settings.json"));
        AppSettings settings = new();
        string? warning = null;
        try
        {
            if (!smokeTest)
            {
                var loaded = await repository.LoadAsync(); settings = loaded.Value; warning = loaded.Warning;
            }
        }
        catch (Exception error) when (error is ContractException or IOException or UnauthorizedAccessException)
        { warning = $"Settings could not be loaded ({error.GetType().Name}). Safe defaults loaded; files preserved."; }
        if (!smokeTest)
        {
            _companions = new CompanionSession();
        }
        _services = CompositionRoot.Build(settings, repository, _companions);
        _services.GetRequiredService<ViewModels.MainViewModel>().StartupWarning = warning;
        ApplyTheme(settings.Theme);
        var window = _services.GetRequiredService<MainWindow>();
        window.ContentRendered += FirstRender;
        void FirstRender(object? sender, EventArgs args)
        {
            window.ContentRendered -= FirstRender;
            _startup.Ready(window.IsVisible && window.WindowState != WindowState.Minimized, smokeTest);
            if (smokeTest) _ = window.Dispatcher.BeginInvoke(new Action(window.Close));
        }
        MainWindow = window;
        window.WindowState = WindowState.Normal;
        window.ShowActivated = true;
        window.Show();
        window.Activate();
        if (!smokeTest)
        {
            await _services.GetRequiredService<ViewModels.PresetsViewModel>().LoadAsync();
            await _services.GetRequiredService<ViewModels.GamesViewModel>().LoadAsync();
        }
        if (_companions is not null) _ = ConnectCompanionsAsync(settings.Services);
    }
    private async Task ConnectCompanionsAsync(OptionalServices services)
    {
        if (_companions is null || _services is null) return;
        await _companions.StartAsync(services, snapshot => MessageBox.Show(MainWindow,
            $"An unfinished display journal from {snapshot.TimestampUtc:g} was found.\n\nRecover only its captured baselines before continuing? Conflicting values will be left for local review.",
            "Chromatic Desktop - recovery", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
        if (_exiting) return;
        var viewModel = _services.GetRequiredService<ViewModels.MainViewModel>();
        if (_companions.Failure is not null) viewModel.StartupWarning = _companions.Failure;
        viewModel.Refresh();
    }
    public static void ApplyTheme(ThemeKind theme)
    {
        if (Current is null) return;
        Current.Resources.MergedDictionaries[0] = new ResourceDictionary
        { Source = new Uri($"pack://application:,,,/Chromatic.App;component/Themes/{theme}.xaml", UriKind.Absolute) };
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _exiting = true; _companions?.Dispose(); _services?.Dispose(); base.OnExit(e);
    }
}
