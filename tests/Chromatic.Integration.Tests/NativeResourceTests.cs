using Chromatic.Agent;
using System.Collections.Immutable;
using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Chromatic.Native;
using Chromatic.Core.Abstractions;
using Chromatic.Infrastructure.Effects;
using Chromatic.Infrastructure.Persistence;
using Xunit;

namespace Chromatic.Integration.Tests;

public sealed class NativeResourceTests
{
    [Fact] public async Task SupersessionDuringFinalSdrProbeCannotReachTheNativeSetter()
    {
        using var directory = new TestDirectory(); using var worker = new AgentStaWorker();
        var api = new MagFake(); var gate = new Gate();
        await worker.InvokeAsync(async () =>
        {
            using var resource = new MagnificationResource(api, () => { gate.Allowed = false; return true; });
            using var owned = new OwnedResourceController(new RecoveryJournalStore(directory.File()),
                new() { UserSid = "S-1-5-21-1", SessionId = 1, AgentPid = 42, AgentStartTicks = 100 }, gate, () => true);
            var result = await owned.ApplyAsync(resource, new(ColorMatrixBuilder.Saturation(180).Values), new(1, 1), default);
            Assert.False(result.Success); Assert.Equal("StaleRevision", result.ErrorCode); Assert.Equal(0, api.Writes);
            Assert.NotNull(owned.Snapshot.Resources[0].PendingValue);
            return true;
        });
    }
    private sealed class Gate : IWriteSafetyGate
    {
        public bool Allowed = true;
        public bool RecoveryReady => true;
        public bool CanWrite(WriteFence fence) => Allowed;
    }
    private static readonly DisplayIdentity Display = new("test-monitor", "serial", @"\\.\DISPLAY7", 1234, 9);
    [Fact] public async Task MagnificationKeepsInitializationReadsWritesAndCleanupOnOneSta()
    {
        using var worker = new AgentStaWorker(); var api = new MagFake();
        await worker.InvokeAsync(async () =>
        {
            using var resource = new MagnificationResource(api, () => true);
            var baseline = await resource.ReadAsync(default); Assert.True(resource.CanOwn(baseline));
            var thread = Environment.CurrentManagedThreadId;
            await Task.Delay(10);
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
            Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
            var desired = resource.Canonicalize(new(ColorMatrixBuilder.Saturation(300).Values));
            await resource.WriteAsync(desired, default);
            Assert.Equal(desired.Values.ToArray(), (await resource.ReadAsync(default)).Values.ToArray());
            await resource.WriteAsync(baseline, default);
            return true;
        });
        Assert.Equal(1, api.Initializations); Assert.Equal(1, api.Uninitializations); Assert.Equal(2, api.Writes);
        Assert.Single(api.Threads.Distinct());
    }
    [Fact] public async Task GlobalHdrBlocksGradingButAllowsClearingOurIdentityBaseline()
    {
        using var worker = new AgentStaWorker(); var api = new MagFake();
        await worker.InvokeAsync(async () =>
        {
            using var resource = new MagnificationResource(api, () => false);
            var baseline = await resource.ReadAsync(default);
            await Assert.ThrowsAsync<NotSupportedException>(() => resource.WriteAsync(new(ColorMatrixBuilder.Saturation(200).Values), default));
            await resource.WriteAsync(baseline, default);
            Assert.False(resource.CanOwn(new(ColorMatrixBuilder.Saturation(120).Values)));
            return true;
        });
        Assert.Equal(1, api.Writes);
    }
    [Fact] public async Task MagnificationFailureIsReportedAndWrongThreadCannotWrite()
    {
        using var worker = new AgentStaWorker(); var api = new MagFake { FailRead = true };
        var resource = new MagnificationResource(api, () => true);
        await worker.InvokeAsync(async () =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => resource.ReadAsync(default)); return true;
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => resource.WriteAsync(new(Matrix5x5.Identity.Values), default));
        await worker.InvokeAsync(() => { resource.Dispose(); return true; }); Assert.Equal(0, api.Writes);
    }
    [Fact] public async Task GammaRequiresCurrentOutputAndSdrAndPreservesExactBaselineBytes()
    {
        var api = new GammaFake(); var mapped = true; var sdr = true;
        using var resource = new GammaResource(Display, () => api, _ => mapped, () => sdr);
        var baseline = await resource.ReadAsync(default); Assert.True(resource.CanOwn(baseline));
        var ramp = GammaRampComposer.Compose(api.Value, 1.2, .4, .3, 0, null);
        var desired = new ResourceValue(ramp.Values.Select(v => (double)v).ToImmutableArray());
        await resource.WriteAsync(desired, default); await resource.WriteAsync(baseline, default);
        Assert.Equal(baseline.Values.ToArray(), (await resource.ReadAsync(default)).Values.ToArray());
        sdr = false; await Assert.ThrowsAsync<NotSupportedException>(() => resource.WriteAsync(desired, default));
        sdr = true; mapped = false; await Assert.ThrowsAsync<NotSupportedException>(() => resource.WriteAsync(desired, default));
        Assert.Equal(2, api.Writes);
    }
    [Fact] public async Task DvcMapsGdiDrivingOutputAndRejectsInvalidatedMappingOrPrivateRange()
    {
        var api = new NvFake(); var mapped = true;
        using var resource = new NvidiaDvcResource(Display, () => api, _ => mapped, () => true);
        var baseline = await resource.ReadAsync(default);
        Assert.Equal(Display.GdiName, api.Name); Assert.Equal(20, baseline.Values[0]);
        Assert.Equal(20, resource.Desired(0).Values[0]); Assert.Equal(90, resource.Desired(1).Values[0]);
        await resource.WriteAsync(resource.Desired(1), default); await resource.WriteAsync(baseline, default);
        mapped = false; await Assert.ThrowsAsync<NotSupportedException>(() => resource.WriteAsync(baseline, default));
        mapped = true; api.Invalid = true;
        await Assert.ThrowsAsync<NotSupportedException>(() => resource.ReadAsync(default)); Assert.Equal(2, api.Writes);
    }
    [Fact] public async Task GammaMeasuresDriverLatencyAndExactBaselineRemainsRestorable()
    {
        var api = new GammaFake { DelayMilliseconds = 275 };
        using var resource = new GammaResource(Display, () => api, _ => true, () => true);
        var baseline = await resource.ReadAsync(default);
        var desired = GammaRampComposer.Compose(api.Value, 1.2);
        await resource.WriteAsync(new(desired.Values.Select(v => (double)v).ToImmutableArray()), default);
        Assert.True(resource.LastWriteDuration >= TimeSpan.FromMilliseconds(250));
        await resource.WriteAsync(baseline, default);
        Assert.Equal(baseline.Values.ToArray(), (await resource.ReadAsync(default)).Values.ToArray());
    }
    private sealed class MagFake : IMagnificationApi
    {
        public int Initializations, Uninitializations, Writes; public bool FailRead;
        public List<int> Threads { get; } = [];
        private MagColorEffect _value = MagColorEffect.FromMatrix(Matrix5x5.Identity);
        public bool Initialize() { Threads.Add(Environment.CurrentManagedThreadId); Initializations++; return true; }
        public bool Read(out MagColorEffect matrix) { Threads.Add(Environment.CurrentManagedThreadId); matrix = _value; return !FailRead; }
        public bool Write(in MagColorEffect matrix) { Threads.Add(Environment.CurrentManagedThreadId); _value = matrix; Writes++; return true; }
        public bool Uninitialize() { Threads.Add(Environment.CurrentManagedThreadId); Uninitializations++; return true; }
    }
    private sealed class GammaFake : IGammaApi
    {
        public GammaRamp Value = new(Enumerable.Range(0, 768).Select(i => (ushort)Math.Round(Math.Pow(i % 256 / 255d, 1.1) * 65535)));
        public int Writes, DelayMilliseconds;
        public GammaRamp Read() => Value;
        public void Write(GammaRamp value) { if (DelayMilliseconds > 0) Thread.Sleep(DelayMilliseconds); Value = value; Writes++; }
        public void Dispose() { }
    }
    private sealed class NvFake : INvApi
    {
        public string? Name; public bool Invalid; public int Level = 20, Writes;
        public nint ResolveDisplay(string gdiName) { Name = gdiName; return 99; }
        public NvDisplayDvcInfo Read(nint display)
        {
            Assert.Equal(99, display);
            return new() { Version = Invalid ? 0u : NvDisplayDvcInfo.Version1, MinLevel = -50, DefaultLevel = 20, MaxLevel = 90, CurrentLevel = Level };
        }
        public void Write(nint display, int level, Func<bool>? stillAuthorized = null)
        { Assert.NotNull(stillAuthorized); Assert.True(stillAuthorized()); Assert.Equal(99, display); Level = level; Writes++; }
        public void Dispose() { }
    }
}
