using Chromatic.Core.Models;
namespace Chromatic.Core.Abstractions;
public interface IGammaBackend
{
    Task<ApplyResult> ApplyAsync(MonitorColorSettings settings, HoldBoostSettings? hold, long revision, CancellationToken cancellationToken);
    Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken);
}
