using System.Windows.Input;
using Chromatic.Core.Models;
using Chromatic.Infrastructure.Persistence;
namespace Chromatic.App.ViewModels;
public sealed class SettingsViewModel : PageViewModel
{
    private readonly AppSessionSettings _session;
    private readonly SettingsRepository _repository;
    private readonly PresetProtocolRegistration? _protocol;
    public string ProtocolStatus => _protocol?.Status ?? "Protocol registration unavailable.";
    public ICommand RegisterProtocolCommand { get; }
    public ICommand UnregisterProtocolCommand { get; }
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
    public SettingsViewModel(AppSessionSettings session, SettingsRepository repository, PresetProtocolRegistration? protocol = null)
        : base("Settings", "Local by default. Explicit by choice.", "Compatibility changes take effect after Save and restart. Phone, startup, Discord, cloud and updates remain inactive.")
    {
        _session = session; _repository = repository; _protocol = protocol;
        void ChangeProtocol(bool register)
        {
            try
            {
                if (_protocol is null) throw new InvalidOperationException("Protocol registration unavailable.");
                if (register) _protocol.Register(); else _protocol.Unregister();
                SaveStatus = "Per-user preset link registration updated.";
            }
            catch (Exception e) { SaveStatus = "Registration rejected: " + e.Message; }
            Changed(nameof(ProtocolStatus));
        }
        RegisterProtocolCommand = new RelayCommand(_ => ChangeProtocol(true));
        UnregisterProtocolCommand = new RelayCommand(_ => ChangeProtocol(false));
        SaveCommand = new AsyncCommand(async () => { await _repository.SaveAsync(_session.Current); SaveStatus = "Settings saved atomically."; },
            e => SaveStatus = $"Save failed ({e.GetType().Name}). Existing data preserved where commit did not occur.");
    }
}
