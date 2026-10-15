using System.Collections.Immutable;
using Chromatic.Core.Models;
namespace Chromatic.Core.Abstractions;
public enum MediaAction { PlayPause, Next, Previous, Seek }
public sealed record MediaSession(string Id, string Title, string Artist, ImmutableHashSet<MediaAction> Supported);
public interface IMediaController
{
    Task<ApplyResult> ControlAsync(string sessionId, MediaAction action, long? positionTicks, CancellationToken cancellationToken);
}
