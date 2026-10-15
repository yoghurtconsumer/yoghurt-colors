using Chromatic.Core.Models;
using Chromatic.Core.State;
using Xunit;
namespace Chromatic.Core.Tests;
public sealed class ProfileResolverTests
{
    [Fact] public void MatchesExactPathAndRejectsAmbiguousRules()
    {
        var identity = new ExecutableIdentity("C:\\Games\\one\\game.exe", 10, 100);
        GameRule rule = new("one", identity.NormalizedPath.ToUpperInvariant(), "p1", true);
        Assert.Equal("p1", ProfileResolver.Resolve(identity, [rule]));
        Assert.Null(ProfileResolver.Resolve(identity, [rule, rule with { Id = "two" }]));
        Assert.Null(ProfileResolver.Resolve(identity with { NormalizedPath = "C:\\Games\\two\\game.exe" }, [rule]));
        Assert.Null(ProfileResolver.Resolve(null, [rule]));
    }
}
