using Chromatic.Core.Models;
namespace Chromatic.Core.Abstractions;
public interface IProfileRepository
{
    Task<DisplayProfile?> LoadAsync(string id, CancellationToken cancellationToken = default);
    Task SaveAsync(DisplayProfile profile, CancellationToken cancellationToken = default);
}
