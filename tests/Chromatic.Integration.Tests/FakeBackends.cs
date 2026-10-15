using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
namespace Chromatic.Integration.Tests;

// Test-only, explicitly injected; never referenced or selected by production DI.
public sealed class FakeColorBackend : IColorBackend
{
    public bool FailApply { get; set; }
    public ColorSettings Current { get; private set; } = new(110);
    public ColorSettings Baseline { get; } = new(110);
    public List<ColorSettings> Applied { get; } = [];
    public int RestoreCount { get; private set; }
    public Task<ApplyResult> ApplyAsync(ColorSettings settings, long revision, CancellationToken cancellationToken)
    {
        if (FailApply) return Task.FromResult(new ApplyResult(false, "FakeColor", "InjectedFailure", "Test driver rejection", revision));
        Current = settings; Applied.Add(settings);
        return Task.FromResult(new ApplyResult(true, "FakeColor", null, "In-memory test write", revision));
    }
    public Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken)
    {
        Current = Baseline; RestoreCount++;
        return Task.FromResult(new ApplyResult(true, "FakeColor", null, "In-memory baseline restored", 0));
    }
}
public sealed class FakeNativeBackends : IVibranceBackend, IGammaBackend, IDisplayBackend, IMonitorBackend
{
    public bool Reject { get; set; }
    public int Calls { get; private set; }
    private Task<ApplyResult> Result(long revision) { Calls++; return Task.FromResult(new ApplyResult(!Reject, "FakeNative", Reject ? "InjectedFailure" : null, "In-memory test operation", revision)); }
    public Task<ApplyResult> ApplyAsync(DisplayIdentity display, double normalizedVibrance, long revision, CancellationToken cancellationToken) => Result(revision);
    public Task<ApplyResult> ApplyAsync(MonitorColorSettings settings, HoldBoostSettings? hold, long revision, CancellationToken cancellationToken) => Result(revision);
    public Task<ApplyResult> PreviewAsync(PreviewTransaction transaction, long revision, CancellationToken cancellationToken) => Result(revision);
    public Task<ApplyResult> SetAsync(DisplayIdentity display, MonitorControl control, uint value, long revision, CancellationToken cancellationToken) => Result(revision);
    public Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken) => Result(0);
    public Task<ApplyResult> UndoAsync(CancellationToken cancellationToken) => Result(0);
}
