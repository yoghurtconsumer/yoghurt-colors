using Chromatic.Core.Models;
namespace Chromatic.Core.Abstractions;
public interface IVibranceBackend
{
    Task<ApplyResult> ApplyAsync(DisplayIdentity display, double normalizedVibrance, long revision, CancellationToken cancellationToken);
    Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken);
}
