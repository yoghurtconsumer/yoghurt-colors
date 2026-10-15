using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
namespace Chromatic.Media;
public sealed class GsmtcController : IMediaController
{
    public Task<ApplyResult> ControlAsync(string sessionId, MediaAction action, long? positionTicks, CancellationToken cancellationToken) =>
        Task.FromResult(ApplyResult.NotImplemented("GSMTC"));
}
