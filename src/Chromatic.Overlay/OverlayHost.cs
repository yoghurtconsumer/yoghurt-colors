using Chromatic.Core.Models;
namespace Chromatic.Overlay;
public sealed class OverlayHost
{
    public Task<ApplyResult> ShowAsync(CrosshairSettings settings, CancellationToken cancellationToken) =>
        Task.FromResult(ApplyResult.NotImplemented("Overlay"));
}
