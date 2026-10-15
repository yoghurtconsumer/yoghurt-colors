using Chromatic.Core.Models;
namespace Chromatic.Core.Abstractions;
public enum MonitorControl { Brightness, Contrast, RedGain, GreenGain, BlueGain }
public interface IMonitorBackend
{
    Task<ApplyResult> SetAsync(DisplayIdentity display, MonitorControl control, uint value, long revision, CancellationToken cancellationToken);
    Task<ApplyResult> UndoAsync(CancellationToken cancellationToken);
}
