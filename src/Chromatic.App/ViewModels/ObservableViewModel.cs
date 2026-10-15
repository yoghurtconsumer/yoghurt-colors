using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Chromatic.App.ViewModels;
public abstract class ObservableViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
public sealed class RelayCommand(Action<object?> action) : ICommand
{
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action(parameter);
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
public sealed class AsyncCommand(Func<Task> action, Action<Exception> failed) : ICommand
{
    private bool _running;
    public bool CanExecute(object? parameter) => !_running;
    public async void Execute(object? parameter)
    {
        if (_running) return;
        _running = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await action(); } catch (Exception e) { failed(e); }
        finally { _running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
    public event EventHandler? CanExecuteChanged;
}
public abstract class PageViewModel(string title, string description, string deferred) : ObservableViewModel
{
    public string Title { get; } = title;
    public string Description { get; } = description;
    public string CapabilityStatus { get; } = "NotImplemented";
    public string Deferred { get; } = deferred;
}
