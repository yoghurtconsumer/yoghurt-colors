using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;
using Chromatic.App.ViewModels;
namespace Chromatic.App;
public sealed class TrayService : IDisposable
{
    private Forms.NotifyIcon? _icon;
    public void Show(Window window, MainViewModel viewModel, Action exit)
    {
        if (_icon is not null) return;
        void Reveal() => Application.Current.Dispatcher.Invoke(() => { window.Show(); window.WindowState = WindowState.Normal; window.Activate(); });
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => Reveal());
        menu.Items.Add("Pause", null, (_, _) => Application.Current.Dispatcher.Invoke(() => viewModel.PauseCommand.Execute(null)));
        menu.Items.Add("Restore", null, (_, _) => Application.Current.Dispatcher.Invoke(() => viewModel.RestoreCommand.Execute(null)));
        menu.Items.Add("Exit", null, (_, _) => Application.Current.Dispatcher.Invoke(exit));
        _icon = new Forms.NotifyIcon { Text = "Chromatic Desktop — effects off", Icon = SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => Reveal();
    }
    public void Hide() { if (_icon is not null) _icon.Visible = false; }
    public void Dispose()
    {
        if (_icon is null) return;
        _icon.Visible = false; _icon.ContextMenuStrip?.Dispose(); _icon.Dispose(); _icon = null;
    }
}
