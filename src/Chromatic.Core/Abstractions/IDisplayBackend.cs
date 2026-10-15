using Chromatic.Core.Models;
namespace Chromatic.Core.Abstractions;
public interface IDisplayBackend
{
    Task<ApplyResult> PreviewAsync(PreviewTransaction transaction, long revision, CancellationToken cancellationToken);
    Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken);
}
