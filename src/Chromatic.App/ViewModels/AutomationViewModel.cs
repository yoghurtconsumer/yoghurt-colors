using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Core.Validation;
using Chromatic.Infrastructure.Ipc;
using Chromatic.Infrastructure.Persistence;

namespace Chromatic.App.ViewModels;

public sealed class AutomationRuleRow(GameRule rule, string name) : ObservableViewModel
{
    public GameRule Rule { get; } = rule;
    public string Label => name + " · " + Rule.ExecutablePath;
    private bool _enabled = rule.Enabled;
    public bool Enabled { get => _enabled; set { _enabled = value; Changed(); } }
}
public sealed class AutomationViewModel : ObservableViewModel
{
    private readonly AutomationRepository _repository;
    private readonly PresetsViewModel _presets;
    private readonly DisplayViewModel _display;
    private readonly IEffectsClient _client;
    private AutomationSettings _saved = new();
    private bool _loaded;
    private readonly SemaphoreSlim _changes = new(1, 1);
    private string _status = "Load configuration before saving. Every listener is off by default.";
    public ObservableCollection<AutomationRuleRow> Rules { get; } = [];
    public ObservableCollection<HotkeyBinding> Hotkeys { get; } = [];
    public AutomationRuleRow? SelectedRule { get; set; }
    public HotkeyBinding? SelectedHotkey { get; set; }
    public PresetDocument? HotkeyPreset { get; set; }
    public IEnumerable<PresetDocument> ColorPresets => _presets.All.Where(p => p.Kind == PresetKind.Color);
    public HotkeyAction[] Actions => Enum.GetValues<HotkeyAction>();
    public HotkeyAction Action { get; set; } = HotkeyAction.Preset;
    public string HotkeyChord { get; set; } = "Ctrl+Alt+F7";
    public string HoldChord { get; set; } = "F6";
    public bool AutomaticProfiles { get; set; }
    public bool HotkeysEnabled { get; set; }
    public bool HoldEnabled { get; set; }
    public bool MouseHolds { get; set; }
    public bool AcceptInjectedInput { get; set; }
    public double Strength { get; set; } = .6;
    public double RedAttenuation { get; set; }
    public double GreenAttenuation { get; set; }
    public double BlueAttenuation { get; set; }
    public string DesktopName => _saved.Desktop.Name;
    public string Status { get => _status; private set { _status = value; Changed(); } }
    public string RuntimeStatus => _client.State.AutomationStatus;
    public ICommand SaveCommand { get; }
    public ICommand RemoveRuleCommand { get; }
    public ICommand AddHotkeyCommand { get; }
    public ICommand RemoveHotkeyCommand { get; }
    public ICommand CaptureDesktopCommand { get; }
    public AutomationViewModel(AutomationRepository repository, PresetsViewModel presets, DisplayViewModel display, IEffectsClient client)
    {
        _repository = repository; _presets = presets; _display = display; _client = client;
        _presets.LibraryChanged += () => Changed(nameof(ColorPresets));
        SaveCommand = new AsyncCommand(SaveAsync, e => Status = "Configuration rejected: " + e.Message);
        RemoveRuleCommand = new RelayCommand(_ => { if (SelectedRule is { } row) Rules.Remove(row); });
        AddHotkeyCommand = new RelayCommand(_ =>
        {
            try
            {
                var id = Enumerable.Range(1, 24).First(i => Hotkeys.All(h => h.Id != i));
                var key = InputChord.Hotkey(id, HotkeyChord, Action, Action == HotkeyAction.Preset ? HotkeyPreset?.Id : null);
                if (Action == HotkeyAction.Preset && HotkeyPreset is null) throw new ArgumentException("Choose a Color preset.");
                Hotkeys.Add(key); Status = "Hotkey drafted. Save to validate and register; conflicts appear in runtime status.";
            }
            catch (Exception e) { Status = "Hotkey rejected: " + e.Message; }
        });
        RemoveHotkeyCommand = new RelayCommand(_ => { if (SelectedHotkey is { } key) Hotkeys.Remove(key); });
        CaptureDesktopCommand = new RelayCommand(_ =>
        {
            _saved = _saved with { Desktop = new() { Id = "desktop", Name = "Desktop color draft", GlobalColor = _display.DraftColor } };
            Changed(nameof(DesktopName)); Status = "Desktop fallback drafted from Display's base color. Save to use it.";
        });
    }
    public void StageRule(SavedGame game)
    {
        var old = Rules.FirstOrDefault(r => r.Rule.Id == game.Id);
        if (old is not null) Rules.Remove(old);
        Rules.Add(new(new(game.Id, game.ExecutablePath, game.PresetId, old?.Enabled ?? false), game.Name));
        Status = "Rule drafted. Check its Enabled box and save configuration. Paths match exactly; no game is launched.";
    }
    public async Task LoadAsync()
    {
        try
        {
            var loaded = await _repository.LoadAsync(); _saved = loaded.Value; _loaded = true;
            AutomaticProfiles = _saved.AutomaticProfiles; HotkeysEnabled = _saved.HotkeysEnabled;
            HoldEnabled = _saved.Boost.Enabled; Strength = _saved.Boost.Strength;
            RedAttenuation = _saved.Boost.RedAttenuation; GreenAttenuation = _saved.Boost.GreenAttenuation; BlueAttenuation = _saved.Boost.BlueAttenuation;
            HoldChord = InputChord.Format(_saved.HoldKeys); MouseHolds = _saved.MouseHolds; AcceptInjectedInput = _saved.AcceptInjectedInput;
            Rules.Clear(); foreach (var rule in _saved.Rules) Rules.Add(new(rule, rule.Id));
            Hotkeys.Clear(); foreach (var key in _saved.Hotkeys) Hotkeys.Add(key);
            foreach (var name in new[] { nameof(AutomaticProfiles), nameof(HotkeysEnabled), nameof(HoldEnabled), nameof(Strength), nameof(RedAttenuation), nameof(GreenAttenuation), nameof(BlueAttenuation), nameof(HoldChord), nameof(MouseHolds), nameof(AcceptInjectedInput), nameof(DesktopName) }) Changed(name);
            Status = loaded.Warning ?? "Configuration loaded. Effects still require explicit Apply; listeners follow saved opt-ins.";
        }
        catch (Exception e) { _loaded = false; Status = "Automation could not load; listeners stay off and saving is disabled: " + e.Message; }
    }
    public async Task SaveAsync()
    {
        await _changes.WaitAsync();
        try
        {
            if (!_loaded) throw new InvalidOperationException("Load configuration successfully first.");
            var rules = Rules.Select(r => r.Rule with { Enabled = r.Enabled }).ToImmutableArray();
            var ids = rules.Select(r => r.ProfileId).Concat(Hotkeys.Where(h => h.Action == HotkeyAction.Preset).Select(h => h.ProfileId!)).Distinct();
            var profiles = ids.Select(id =>
            {
                var preset = ColorPresets.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException("A rule/hotkey references a removed preset. Replace or remove that rule/hotkey.");
                return new DisplayProfile { Id = preset.Id, Name = preset.Name, GlobalColor = preset.Color! };
            }).ToImmutableArray();
            var next = _saved with { AutomaticProfiles = AutomaticProfiles, HotkeysEnabled = HotkeysEnabled,
                Boost = new(HoldEnabled, Strength, RedAttenuation, GreenAttenuation, BlueAttenuation),
                HoldKeys = InputChord.Parse(HoldChord), MouseHolds = MouseHolds, AcceptInjectedInput = AcceptInjectedInput,
                Rules = rules, Profiles = profiles, Hotkeys = Hotkeys.ToImmutableArray() };
            AutomationValidator.Validate(next);
            // A valid disk document must also fit its bounded authenticated IPC command.
            _ = Protocol.Encode(new IpcRequest(1, "command", Process.GetCurrentProcess().SessionId,
                new(Guid.NewGuid(), _client.State.Revision, _client.State.Generation, new ConfigureAutomation(next))));
            try { await _repository.SaveAsync(next); }
            catch { await LoadAsync(); throw; }
            _saved = next; await ApplySavedAsync();
        }
        finally { _changes.Release(); }
    }
    public async Task ApplySavedAsync()
    {
        if (!_loaded) return;
        var state = _client.State;
        var ack = await _client.SendAsync(new(Guid.NewGuid(), state.Revision, state.Generation, new ConfigureAutomation(_saved)));
        Status = ack.Decision.Status == CompletionStatus.Accepted ? "Configuration saved and sent. Check runtime status for listener registration/conflicts. Effects remain under Apply/Pause/Restore control." :
            "Saved locally; Agent did not accept configuration: " + ack.Decision.Explanation;
        Refresh();
    }
    public void Refresh() => Changed(nameof(RuntimeStatus));
}
