using Chromatic.Core.Models;
namespace Chromatic.Remote;
public sealed class PhoneServer
{
    public bool IsRunning => false;
    public Task<ApplyResult> StartAsync(CancellationToken cancellationToken) => Task.FromResult(ApplyResult.NotImplemented("Remote"));
}
