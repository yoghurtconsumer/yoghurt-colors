namespace Chromatic.Integration.Tests;
internal sealed class TestDirectory : IDisposable
{
    public string Root { get; } = Path.Combine(Environment.GetEnvironmentVariable("CHROMATIC_TEST_ROOT") ??
        Path.Combine(Path.GetTempPath(), "Chromatic.Stage1.Tests"), Guid.NewGuid().ToString("N"));
    public TestDirectory() => Directory.CreateDirectory(Root);
    public string File(string name = "settings.json") => Path.Combine(Root, name);
    public void Dispose() => Directory.Delete(Root, recursive: true);
}
