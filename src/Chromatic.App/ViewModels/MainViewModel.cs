using System.Collections.Immutable;
using System.Windows.Input;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;

namespace Chromatic.App.ViewModels;
public sealed record NavigationItem(string Label, string Glyph, PageViewModel Page);
public sealed class MainViewModel : ObservableViewModel
{
    private readonly IEffectsClient _client;
    private NavigationItem _selected;
    private string _status = "Effects off · Stage 2 development build";
    private readonly DisplayViewModel _display;
    public ImmutableArray<NavigationItem> Navigation { get; }
    public MusicViewModel Music { get; }
    public ImmutableArray<BackendCapability> Capabilities => _client.State.Capabilities.Backends;
    private string? _startupWarning;
    public string? StartupWarning { get => _startupWarning; set { _startupWarning = value; Changed(); } }
    public string Status { get => _status; private set { _status = value; Changed(); } }
    public NavigationItem Selected { get => _selected; set { _selected = value; Changed(); Changed(nameof(CurrentPage)); } }
    public PageViewModel CurrentPage => Selected.Page;
    public ICommand RestoreCommand { get; }
    public ICommand PauseCommand { get; }
    public MainViewModel(IEffectsClient client, DisplayViewModel display, PresetsViewModel presets,
        GamesViewModel games, ResolutionViewModel resolution, CrosshairViewModel crosshair,
        MonitorViewModel monitor, CustomizeViewModel customize, SettingsViewModel settings, MusicViewModel music)
    {
        _client = client; Music = music; _display = display;
        Navigation = [new("Display", "◐", display), new("Presets / Library", "▦", presets), new("Games", "◇", games),
            new("Resolution", "▱", resolution), new("Crosshair", "+", crosshair), new("Monitor", "▣", monitor),
            new("Customize", "◈", customize), new("Settings", "⚙", settings)];
        _selected = Navigation[0];
        RestoreCommand = new AsyncCommand(() => SendAsync(new Restore()), e => Status = e.Message);
        PauseCommand = new AsyncCommand(() => SendAsync(new Suspend(SuspensionReason.Pause)), e => Status = e.Message);
    }
    private async Task SendAsync(EffectsCommand command)
    {
        var state = _client.State;
        var ack = await _client.SendAsync(new(Guid.NewGuid(), state.Revision, state.Generation, command));
        Status = $"{ack.Completion.ErrorCode ?? "Completed"}: {ack.Completion.Explanation}";
    }
    public Task ShutdownAsync() => SendAsync(new Shutdown());
    public void Refresh()
    {
        Changed(nameof(Capabilities)); _display.Refresh();
        if (_client is Chromatic.Infrastructure.Ipc.CompanionSession session && session.Failure is not null) Status = session.Failure;
        else if (_client.State.Observed.LastResult is { } observed) Status = $"{observed.ErrorCode ?? "Completed"}: {observed.Explanation}";
    }
}
