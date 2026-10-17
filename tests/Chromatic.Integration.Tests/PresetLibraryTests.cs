using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chromatic.Core.Models;
using Chromatic.Infrastructure.Persistence;
using Xunit;

namespace Chromatic.Integration.Tests;

public sealed class PresetLibraryTests
{
    public static IEnumerable<object[]> Documents()
    {
        var intent = new DisplayIntent(1920, 1080, new(144, 1));
        yield return [new PresetDocument { Color = new(300), Name = "Maximum saturation", AuthorMetadata = new("Local author", DateTimeOffset.UnixEpoch) }];
        yield return [new PresetDocument { Kind = PresetKind.Crosshair, Color = null, Crosshair = new() }];
        yield return [new PresetDocument { Kind = PresetKind.Resolution, Color = null, DisplayIntent = intent }];
        yield return [new PresetDocument { Kind = PresetKind.FullSetup, Crosshair = new(), DisplayIntent = intent }];
    }
    [Theory] [MemberData(nameof(Documents))]
    public void AllTypedDocumentsRoundTripOfflineAndLinksDoNotApplyAnything(PresetDocument preset)
    {
        var original = PresetExchange.Encode(preset);
        Assert.Equal(original, PresetExchange.Encode(PresetExchange.Decode(original)));
        var code = PresetExchange.Share(preset);
        Assert.StartsWith("CD1-", code);
        Assert.Equal(original, PresetExchange.Encode(PresetExchange.ImportCode(code)));
        Assert.Equal(original, PresetExchange.Encode(PresetExchange.ParseLink("chromaticdesk://preset/" + code)));
        Assert.Contains("nothing is applied automatically", PresetExchange.Diff(preset, new()));
    }
    [Theory]
    [InlineData("chromaticdesk://preset/CD1-a?apply=true")]
    [InlineData("chromaticdesk://evil/CD1-a")]
    [InlineData("chromaticdesk://user@preset/CD1-a")]
    [InlineData("chromaticdesk://preset:22/CD1-a")]
    [InlineData("chromaticdesk://preset/%43D1-a")]
    [InlineData("https://preset/CD1-a")]
    [InlineData("chromaticdesk://preset/CD1-a/extra")]
    public void RejectsForeignOrAmbiguousLinks(string link) => Assert.Throws<ContractException>(() => PresetExchange.ParseLink(link));
    [Fact] public void RejectsChecksumDamageAndCompressedBombBeforeJsonParsing()
    {
        var code = PresetExchange.Share(new());
        code = code[..^1] + (code[^1] == '0' ? "1" : "0");
        Assert.Contains("checksum", Assert.Throws<ContractException>(() => PresetExchange.ImportCode(code)).Message);
        var large = Encoding.UTF8.GetBytes(new string('x', PresetExchange.MaxBytes + 1));
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, true)) gzip.Write(large);
        var bomb = "CD1-" + Convert.ToBase64String(compressed.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_') + "." + Convert.ToHexString(SHA256.HashData(large)).ToLowerInvariant();
        Assert.Contains("64 KiB", Assert.Throws<ContractException>(() => PresetExchange.ImportCode(bomb)).Message);
        Assert.Throws<ContractException>(() => PresetExchange.ImportCode(new string('x', PresetExchange.MaxCodeLength + 1)));
    }
    [Fact] public void StrictImportsRejectUnknownFieldsDuplicateKeysBadRangesAndFutureVersions()
    {
        var json = Encoding.UTF8.GetString(PresetExchange.Encode(new()));
        foreach (var invalid in new[] { json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99"),
            json.Replace("\"saturationPercent\": 100", "\"saturationPercent\": 301"),
            json.Replace("\"id\":", "\"extra\": true, \"id\":"),
            json.Replace("\"id\":", "\"id\": \"duplicate\", \"id\":") })
            Assert.Throws<ContractException>(() => PresetExchange.Decode(Encoding.UTF8.GetBytes(invalid)));
    }
    [Fact] public async Task LibraryDefaultsDoNotWriteAndCorruptionCannotDestroyLastGood()
    {
        using var folder = new TestDirectory(); var path = folder.File(); var repository = new LibraryRepository(path);
        var first = await repository.LoadAsync(); Assert.Equal(7, first.Value.Presets.Length); Assert.False(File.Exists(path));
        await repository.SaveAsync(first.Value);
        await repository.SaveAsync(first.Value with { Presets = [] });
        await File.WriteAllTextAsync(path, "{damaged");
        var recovered = await repository.LoadAsync(); Assert.Equal(LoadOrigin.LastGood, recovered.Origin); Assert.Equal(7, recovered.Value.Presets.Length);
        await Assert.ThrowsAnyAsync<Exception>(() => repository.SaveAsync(new()));
        Assert.Equal("{damaged", await File.ReadAllTextAsync(path));
    }
    [Theory] [InlineData(WriteCheckpoint.BeforeFlush)] [InlineData(WriteCheckpoint.BeforeReplace)]
    public async Task InterruptedLibrarySavePreservesThePreviousCompleteLibrary(WriteCheckpoint checkpoint)
    {
        using var folder = new TestDirectory(); var path = folder.File(); var library = new PresetLibrary { Presets = BuiltInPresets.Create() };
        await new LibraryRepository(path).SaveAsync(library);
        await Assert.ThrowsAsync<IOException>(() => new LibraryRepository(path, new Fault(checkpoint)).SaveAsync(new()));
        Assert.Equal(7, (await new LibraryRepository(path).LoadAsync()).Value.Presets.Length);
        Assert.Empty(Directory.GetFiles(folder.Root, "*.tmp"));
    }
    [Fact] public async Task LibraryRejectsDuplicateIdsAndFutureVersionWithoutWriting()
    {
        using var folder = new TestDirectory(); var repository = new LibraryRepository(folder.File());
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(new() { Presets = [new(), new()] }));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(new() { SchemaVersion = 2 }));
        Assert.False(File.Exists(folder.File()));
    }
    [Fact] public async Task OversizedFileIsRejectedWithoutReadingItsPayload()
    {
        using var folder = new TestDirectory(); await File.WriteAllTextAsync(folder.File(), new string(' ', PresetExchange.MaxBytes + 1));
        await Assert.ThrowsAsync<ContractException>(() => PresetExchange.ReadFileAsync(folder.File()));
    }
    private sealed class Fault(WriteCheckpoint at) : IWriteFaultInjector
    { public void Check(WriteCheckpoint checkpoint) { if (at == checkpoint) throw new IOException("Injected library failure."); } }
}
