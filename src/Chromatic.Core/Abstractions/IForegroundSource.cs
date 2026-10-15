using Chromatic.Core.Models;
namespace Chromatic.Core.Abstractions;
public interface IForegroundSource
{
    IAsyncEnumerable<ExecutableIdentity?> WatchAsync(CancellationToken cancellationToken);
}
