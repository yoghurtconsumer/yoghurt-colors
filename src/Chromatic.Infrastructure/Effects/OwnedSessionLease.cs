namespace Chromatic.Infrastructure.Effects;

// The OS closes this exclusive handle on process death. Recovery cannot take it
// until the verified Agent is dead and the previous writer's handle is gone.
public sealed class OwnedSessionLease : IDisposable
{
    private FileStream? _lease;
    public bool IsHeld => _lease is not null;
    public OwnedSessionLease(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _lease = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    public void Dispose() => Interlocked.Exchange(ref _lease, null)?.Dispose();
}
