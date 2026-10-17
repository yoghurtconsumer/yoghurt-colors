using Chromatic.Core.Models;
using Chromatic.Infrastructure.Discovery;
using Chromatic.Infrastructure.Persistence;
using Xunit;

namespace Chromatic.Integration.Tests;

public sealed class SteamDiscoveryTests
{
    [Fact] public void FindsOnlyManifestMetadataWithoutOpeningOrScanningGameDirectories()
    {
        using var folder = new TestDirectory(); var apps = Path.Combine(folder.Root, "steamapps"); Directory.CreateDirectory(apps);
        var second = Path.Combine(folder.Root, "other-library"); Directory.CreateDirectory(Path.Combine(second, "steamapps"));
        File.WriteAllText(Path.Combine(apps, "libraryfolders.vdf"), "\"libraryfolders\" { \"0\" { \"path\" \"" + second.Replace("\\", "\\\\") + "\" } }");
        File.WriteAllText(Path.Combine(second, "steamapps", "appmanifest_1.acf"), "\"AppState\" { \"appid\" \"1\" \"name\" \"Test game\" \"installdir\" \"No game files here\" }");
        File.WriteAllText(Path.Combine(apps, "appmanifest_2.acf"), "\"AppState\" { \"appid\" \"2\" \"name\" \"Escape\" \"installdir\" \"../escape\" }");
        File.WriteAllText(Path.Combine(apps, "appmanifest_3.acf"), "{ broken");
        var game = Assert.Single(SteamDiscovery.Discover(folder.Root));
        Assert.Equal("Test game", game.Name); Assert.Equal("1", game.AppId); Assert.False(Directory.Exists(game.InstallDirectory));
    }
    [Theory]
    [InlineData("\"x\" { \"a\" \"1\" \"A\" \"2\" }")]
    [InlineData("\"x\" { \"a\" \"1\"")]
    [InlineData("x unquoted")]
    public void MalformedVdfFailsWithBounds(string input) => Assert.Throws<InvalidDataException>(() => Vdf.Parse(input));
    [Fact] public void RejectsDeepOversizedOrExcessiveVdfBeforeDiscovery()
    {
        Assert.Throws<InvalidDataException>(() => Vdf.Parse(string.Concat(Enumerable.Repeat("\"x\" {", 18)) + new string('}', 18)));
        Assert.Throws<InvalidDataException>(() => Vdf.Parse(new string(' ', 1048577)));
        using var folder = new TestDirectory(); var apps = Path.Combine(folder.Root, "steamapps"); Directory.CreateDirectory(apps);
        for (var i = 0; i < 513; i++) File.WriteAllText(Path.Combine(apps, $"appmanifest_{i}.acf"), "invalid");
        Assert.Throws<InvalidDataException>(() => SteamDiscovery.Discover(folder.Root));
    }
    [Theory]
    [InlineData(@"\\server\game.exe")]
    [InlineData(@"\\?\C:\game.exe")]
    [InlineData(@"C:\game.exe:stream.exe")]
    [InlineData(@"relative\game.exe")]
    [InlineData(@"C:\game.txt")]
    public void GameBookmarksRejectNonLocalOrAmbiguousExecutablePaths(string path) => Assert.Throws<ArgumentException>(() => SteamDiscovery.NormalizeExecutable(path));
    [Fact] public async Task GameBookmarksValidateCanonicalPathsAndCaseInsensitiveDuplicates()
    {
        using var folder = new TestDirectory(); var repository = new GameLibraryRepository(folder.File());
        var game = new SavedGame("one", "Game", @"C:\Games\Siege.exe", "preset");
        await repository.SaveAsync(new() { Games = [game] });
        Assert.Equal(game, Assert.Single((await repository.LoadAsync()).Value.Games));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(new() { Games = [game, game with { Id = "two", ExecutablePath = @"c:\games\SIEGE.exe" }] }));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(new() { Games = [game with { ExecutablePath = @"C:\Games\..\Siege.exe" }] }));
    }
}
