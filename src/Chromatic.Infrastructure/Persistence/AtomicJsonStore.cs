using System.Collections.Concurrent;
using System.Text.Json;

namespace Chromatic.Infrastructure.Persistence;

public enum LoadOrigin { Primary, LastGood, Default }
public sealed record LoadResult<T>(T Value, LoadOrigin Origin, string? Warning);
public enum WriteCheckpoint { BeforeFlush, BeforeReplace, AfterReplace }
public interface IWriteFaultInjector { void Check(WriteCheckpoint checkpoint); }
public sealed class NoWriteFaults : IWriteFaultInjector { public void Check(WriteCheckpoint checkpoint) { } }

public sealed class AtomicJsonStore<T>(string path, string schema, Action<T> validate,
    Func<T> defaults, IWriteFaultInjector? faults = null)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path = Path.GetFullPath(path);
    private readonly IWriteFaultInjector _faults = faults ?? new NoWriteFaults();
    private string Backup => _path + ".last-good";
    public async Task<LoadResult<T>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var gate = Gates.GetOrAdd(_path, _ => new(1, 1));
        await gate.WaitAsync(cancellationToken);
        try { return await LoadUnlocked(cancellationToken); }
        finally { gate.Release(); }
    }
    private async Task<LoadResult<T>> LoadUnlocked(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path) && !File.Exists(Backup))
        {
            var initial = defaults(); validate(initial);
            using var document = JsonContract.Parse(JsonSerializer.SerializeToUtf8Bytes(initial, JsonContract.Options));
            JsonContract.Validate(schema, document.RootElement);
            return new(initial, LoadOrigin.Default, null);
        }
        Exception? failure = null;
        try { return new(await Read(_path, cancellationToken), LoadOrigin.Primary, null); }
        catch (Exception e) when (IsDataFailure(e)) { failure = e; }
        try { return new(await Read(Backup, cancellationToken), LoadOrigin.LastGood,
            $"Primary invalid/unavailable: {failure!.GetType().Name}. Last-good data loaded; primary has not been overwritten."); }
        catch (Exception e) when (IsDataFailure(e))
        { throw new ContractException($"Primary and last-good are invalid/unavailable ({failure!.GetType().Name}, {e.GetType().Name}); files preserved."); }
    }
    private static bool IsDataFailure(Exception e) => e is IOException or UnauthorizedAccessException or
        JsonException or ContractException or ArgumentException or FormatException or InvalidOperationException;
    private async Task<T> Read(string filename, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > 1_048_576) throw new ContractException("File exceeds 1 MiB.");
        var bytes = new byte[checked((int)file.Length)];
        await file.ReadExactlyAsync(bytes, cancellationToken);
        var migrated = MigrationRunner.ToCurrent(schema, bytes);
        using var document = JsonContract.Parse(migrated);
        JsonContract.Validate(schema, document.RootElement);
        var value = document.RootElement.Deserialize<T>(JsonContract.Options) ?? throw new ContractException("Null document.");
        validate(value);
        return value;
    }
    public async Task SaveAsync(T value, CancellationToken cancellationToken = default)
    {
        validate(value);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonContract.Options);
        using (var document = JsonContract.Parse(bytes)) JsonContract.Validate(schema, document.RootElement);
        var gate = Gates.GetOrAdd(_path, _ => new(1, 1));
        await gate.WaitAsync(cancellationToken);
        string? temporary = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            // Cross-process exclusion. Contention fails explicitly; never writes concurrently.
            await using var lease = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var exists = File.Exists(_path);
            if (exists)
            {
                // Never rotate corruption over the last-good backup. Caller must review/recover it first.
                _ = await Read(_path, cancellationToken);
            }
            temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await output.WriteAsync(bytes, cancellationToken);
                _faults.Check(WriteCheckpoint.BeforeFlush);
                output.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            _faults.Check(WriteCheckpoint.BeforeReplace);
            if (exists) File.Replace(temporary, _path, Backup, ignoreMetadataErrors: false);
            else File.Move(temporary, _path); // same-directory rename; no partial destination
            temporary = null;
            _faults.Check(WriteCheckpoint.AfterReplace); // commit may have succeeded; re-read authoritative data
        }
        finally
        {
            try { if (temporary is not null) File.Delete(temporary); }
            finally { gate.Release(); }
        }
    }
}
