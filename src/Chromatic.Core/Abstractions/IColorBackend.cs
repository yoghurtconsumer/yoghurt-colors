using Chromatic.Core.Models;
namespace Chromatic.Core.Abstractions;
public interface IColorBackend
{
    Task<ApplyResult> ApplyAsync(ColorSettings settings, long revision, CancellationToken cancellationToken);
    Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken);
}
