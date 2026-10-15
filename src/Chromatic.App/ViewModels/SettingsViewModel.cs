using System.Windows.Input;
using Chromatic.Core.Models;
using Chromatic.Infrastructure.Persistence;
namespace Chromatic.App.ViewModels;
public sealed class SettingsViewModel : PageViewModel
{
    private readonly AppSessionSettings _session;
    private readonly SettingsRepository _repository;
    private string _saveStatus = "Close exits and requests restore by default. Optional services are off.";
    public bool CloseToTray
    {
        get => _session.Current.CloseBehavior == CloseBehavior.MinimizeToTray;
        set { _session.Current = _session.Current with { CloseBehavior = value ? CloseBehavior.MinimizeToTray : CloseBehavior.ExitAndRestore }; Changed(); }
    }
    public string SaveStatus { get => _saveStatus; private set { _saveStatus = value; Changed(); } }
    public bool GammaCompatibility
    {
        get => _session.Current.Services.GammaCompatibility;
        set { _session.Current = _session.Current with { Services = _session.Current.Services with { GammaCompatibility = value } }; Changed(); }
    }
    public bool NvidiaExperimental
    {
        get => _session.Current.Services.NvidiaExperimental;
        set { _session.Current = _session.Current with { Services = _session.Current.Services with { NvidiaExperimental = value } }; Changed(); }
    }
    public ICommand SaveCommand { get; }
    public SettingsViewModel(AppSessionSettings session, SettingsRepository repository)
        : base("Settings", "Local by default. Explicit by choice.", "Compatibility changes take effect after Save and restart. Phone, startup, Discord, cloud and updates remain inactive.")
    {
        _session = session; _repository = repository;
        SaveCommand = new AsyncCommand(async () => { await _repository.SaveAsync(_session.Current); SaveStatus = "Settings saved atomically."; },
            e => SaveStatus = $"Save failed ({e.GetType().Name}). Existing data preserved where commit did not occur.");
    }
}
