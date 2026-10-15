using System.Collections.Immutable;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Color;
using Chromatic.Core.Models;

namespace Chromatic.Native;

public sealed class MagnificationResource(IMagnificationApi api, Func<bool> verifiedSdr) : IResourceAdapter, IDisposable
{
    private int _thread;
    private bool _initialized;
    public ResourceDescriptor Descriptor { get; } = new(OwnedResourceKind.GlobalMatrix, EffectScope.GlobalDesktop);
    public ResourceValue Canonicalize(ResourceValue value) => new(new Matrix5x5(value.Values).ToSinglePrecision().Select(v => (double)v).ToImmutableArray());
    public bool CanOwn(ResourceValue baseline) => baseline.Values.SequenceEqual(Matrix5x5.Identity.Values);
    public Task<ResourceValue> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_initialized)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) throw new InvalidOperationException("Magnification requires its Agent STA thread.");
            _thread = Environment.CurrentManagedThreadId;
            if (!api.Initialize()) throw new NotSupportedException("Magnification initialization failed.");
            _initialized = true;
        }
        RequireThread();
        if (!api.Read(out var native)) throw new InvalidOperationException("Magnification read failed; no reliable extended error is promised.");
        return Task.FromResult(new ResourceValue(native.ToMatrix().Values));
    }
    public Task WriteAsync(ResourceValue value, CancellationToken cancellationToken) => WriteAsync(value, cancellationToken, () => true);
    public Task WriteAsync(ResourceValue value, CancellationToken cancellationToken, Func<bool> stillAuthorized)
    {
        cancellationToken.ThrowIfCancellationRequested(); RequireThread();
        // Clearing our matrix back to its verified identity baseline remains safe
        // when HDR is enabled externally. Never grade an unverified desktop.
        if (!value.Values.SequenceEqual(Matrix5x5.Identity.Values) && !verifiedSdr())
            throw new NotSupportedException("Global SDR is not verified; HDR/advanced-color outputs must suspend the matrix.");
        var native = MagColorEffect.FromMatrix(new(value.Values));
        if (!stillAuthorized()) throw new WriteFenceRejectedException();
        if (!api.Write(in native)) throw new InvalidOperationException("Magnification setter returned failure.");
        return Task.CompletedTask;
    }
    private void RequireThread()
    {
        if (!_initialized || Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Magnification call is outside its owning STA thread.");
    }
    public void Dispose()
    {
        if (!_initialized) return;
        RequireThread();
        _initialized = false;
        if (!api.Uninitialize()) throw new InvalidOperationException("MagUninitialize returned failure.");
    }
}

public sealed class GammaResource(DisplayIdentity display, Func<IGammaApi> createApi,
    Func<DisplayIdentity, bool> mappingIsCurrent, Func<bool> verifiedSdr) : IResourceAdapter, IDisposable
{
    private IGammaApi? _api;
    public TimeSpan? LastWriteDuration { get; private set; }
    public ResourceDescriptor Descriptor { get; } = new(OwnedResourceKind.GammaRamp, EffectScope.PerMonitor, display);
    public ResourceValue Canonicalize(ResourceValue value)
    {
        if (value.Values.Length != 768 || value.Values.Any(v => !double.IsFinite(v) || v < 0 || v > 65535 || v != Math.Truncate(v)))
            throw new ArgumentException("Gamma values must be 768 exact WORDs.");
        return value;
    }
    public bool CanOwn(ResourceValue baseline)
    {
        _ = Canonicalize(baseline);
        for (var channel = 0; channel < 3; channel++)
            for (var i = 1; i < 256; i++) if (baseline.Values[channel * 256 + i] < baseline.Values[channel * 256 + i - 1]) return false;
        return true;
    }
    public Task<ResourceValue> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); RequireMapping();
        _api ??= createApi();
        var read = new ResourceValue(_api.Read().Values.Select(v => (double)v).ToImmutableArray());
        return Task.FromResult(read);
    }
    public Task WriteAsync(ResourceValue value, CancellationToken cancellationToken) => WriteAsync(value, cancellationToken, () => true);
    public Task WriteAsync(ResourceValue value, CancellationToken cancellationToken, Func<bool> stillAuthorized)
    {
        cancellationToken.ThrowIfCancellationRequested(); RequireMapping(); _ = Canonicalize(value);
        if (!verifiedSdr()) throw new NotSupportedException("SDR gamma disabled for unverified HDR/ACM state.");
        _api ??= createApi();
        var ramp = new GammaRamp(value.Values.Select(v => (ushort)v));
        if (!stillAuthorized()) throw new WriteFenceRejectedException();
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try { _api.Write(ramp); }
        finally { LastWriteDuration = System.Diagnostics.Stopwatch.GetElapsedTime(started); }
        return Task.CompletedTask;
    }
    private void RequireMapping()
    {
        if (!mappingIsCurrent(display)) throw new NotSupportedException("Display mapping is missing, stale or ambiguous.");
    }
    public void Dispose() { _api?.Dispose(); _api = null; }
}

public sealed class NvidiaDvcResource(DisplayIdentity display, Func<INvApi> createApi,
    Func<DisplayIdentity, bool> mappingIsCurrent, Func<bool> verifiedSdr) : IResourceAdapter, IDisposable
{
    private INvApi? _api;
    private nint _handle;
    private NvDisplayDvcInfo _range;
    public ResourceDescriptor Descriptor { get; } = new(OwnedResourceKind.NativeVibrance, EffectScope.PerMonitor, display);
    public ResourceValue Canonicalize(ResourceValue value)
    {
        if (value.Values.Length != 1 || !double.IsFinite(value.Values[0]) || value.Values[0] != Math.Truncate(value.Values[0])
            || value.Values[0] < int.MinValue || value.Values[0] > int.MaxValue) throw new ArgumentException("Invalid DVC integer.");
        return value;
    }
    public bool CanOwn(ResourceValue baseline) => _range.IsValid;
    public ResourceValue Desired(double normalizedVibrance)
    {
        Chromatic.Core.Validation.SettingsValidator.Range(normalizedVibrance, -1, 1, nameof(normalizedVibrance));
        return new([(double)_range.Map((normalizedVibrance + 1) / 2)]);
    }
    public Task<ResourceValue> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); RequireMapping();
        _api ??= createApi();
        if (_handle == 0) _handle = _api.ResolveDisplay(display.GdiName);
        _range = _api.Read(_handle);
        if (!_range.IsValid) throw new NotSupportedException("Incompatible DVC range; private adapter disabled.");
        return Task.FromResult(new ResourceValue([(double)_range.CurrentLevel]));
    }
    public Task WriteAsync(ResourceValue value, CancellationToken cancellationToken) => WriteAsync(value, cancellationToken, () => true);
    public Task WriteAsync(ResourceValue value, CancellationToken cancellationToken, Func<bool> stillAuthorized)
    {
        cancellationToken.ThrowIfCancellationRequested(); RequireMapping(); _ = Canonicalize(value);
        if (!verifiedSdr()) throw new NotSupportedException("DVC requires a qualified SDR display.");
        if (_api is null || _handle == 0) throw new InvalidOperationException("Read baseline before DVC write.");
        if (!stillAuthorized()) throw new WriteFenceRejectedException();
        _api.Write(_handle, (int)value.Values[0], stillAuthorized);
        return Task.CompletedTask;
    }
    private void RequireMapping()
    { if (!mappingIsCurrent(display)) throw new NotSupportedException("NVIDIA display-driving output mapping is stale, missing or ambiguous."); }
    public void Dispose() { _api?.Dispose(); _api = null; _handle = 0; }
}
