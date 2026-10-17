using Chromatic.Core.Models;
using Chromatic.Core.State;
using Xunit;
namespace Chromatic.Core.Tests;
public sealed class DisplayQueryTests
{
    [Theory] [InlineData(-1)] [InlineData(2049)]
    public void InvalidPageOffsetsAreRejectedWithoutChangingEffects(int offset)
    {
        var state = new EffectsState { Enabled = true, Held = true, Revision = 8, Generation = 9 };
        var result = EffectsReducer.Reduce(state, new(Guid.NewGuid(), 0, 0, new QueryDisplays(Guid.NewGuid(), Offset: offset)));
        Assert.Equal(CompletionStatus.Invalid, result.Result.Status); Assert.Same(state, result.State);
    }
    [Fact] public void FreshReadIgnoresWriteRevisionButNeverClearsHoldOrAdvancesGeneration()
    {
        var state = new EffectsState { Enabled = true, Held = true, Revision = 8, Generation = 9 };
        var result = EffectsReducer.Reduce(state, new(Guid.NewGuid(), 0, 0, new QueryDisplays()));
        Assert.Equal(CompletionStatus.Accepted, result.Result.Status); Assert.Same(state, result.State);
        Assert.Equal(CompletionStatus.Invalid, EffectsReducer.Reduce(state, new(Guid.NewGuid(), 0, 0, new QueryDisplays(Offset: 4))).Result.Status);
    }
}
