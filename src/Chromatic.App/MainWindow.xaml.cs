using System.ComponentModel;
using System.Windows;
using Chromatic.App.ViewModels;
using Chromatic.Core.Models;
namespace Chromatic.App;
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly AppSessionSettings _settings;
    private readonly TrayService _tray;
    private bool _exiting;
    private bool _shutdownComplete;
    private bool _shutdownInProgress;
    public MainWindow(MainViewModel viewModel, AppSessionSettings settings, TrayService tray)
    {
        InitializeComponent(); DataContext = _viewModel = viewModel; _settings = settings; _tray = tray;
    }
    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_shutdownComplete) return;
        e.Cancel = true;
        if (!_exiting && _settings.Current.CloseBehavior == CloseBehavior.MinimizeToTray)
        {
            _tray.Show(this, _viewModel, () => { _exiting = true; Close(); }); Hide(); return;
        }
        if (_shutdownInProgress) return;
        _shutdownInProgress = true;
        try { await _viewModel.ShutdownAsync(); }
        finally { _shutdownComplete = true; _tray.Dispose(); _ = Dispatcher.BeginInvoke(new Action(Close)); }
    }
}
