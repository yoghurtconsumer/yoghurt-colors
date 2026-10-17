using System.Collections.Immutable;
using System.Text.Json;
using Chromatic.Agent;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Ipc;
using Chromatic.Native;
using Xunit;

namespace Chromatic.Integration.Tests;
public sealed class DisplayInventoryTests
{
    private static DisplayOutputInfo Output(uint target = 9, uint source = 2, int x = -1920) => new(
        new("monitor-" + target, null, @"\\.\DISPLAY" + (target + 1), 42, target), "Monitor " + target, source, 42, x, -100,
        new(1920, 1080, new(60000, 1001)), 1, 1, false, false, "SDR", "Injected read-only");
    private sealed class Api : IDisplayInventoryApi
    {
        public Queue<int> Statuses { get; } = new(); public int Queries, Enumerations;
        public bool Changed; public uint PathCount = 2;
        public ImmutableArray<DisplayOutputInfo> Outputs = [Output(), Output(10, 3, 0)];
        public ImmutableArray<EnumeratedDisplayMode> Modes = [new(0, new(1920, 1080, new(60, 1)), false, true, 0),
            new(1, new(1280, 960, new(144, 1)), false, true, 0), new(2, new(1920, 1080, new(60, 1), 16), false, true, 0),
            new(3, new(1920, 1080, new(144, 1)), true, true, 0), new(4, new(800, 600, new(60, 1)), false, false, 0),
            new(5, new(1024, 768, new(60, 1)), false, true, 32)];
        public DisplayBufferCounts Counts() => new(0, PathCount, 4);
        public DisplayPathQuery Query(uint paths, uint modes)
        {
            Queries++; var status = Statuses.Count == 0 ? 0 : Statuses.Dequeue();
            return new(status, Changed && Queries > 1 ? Outputs.SetItem(0, Outputs[0] with { X = 123 }) : Outputs);
        }
        public ImmutableArray<EnumeratedDisplayMode> Enumerate(string gdi) { Enumerations++; return Modes; }
    }
    [Fact] public void ReaderPreservesNegativePositionsIdsAndFractionalRefreshAndFiltersUnsafeModes()
    {
        var api = new Api(); var snapshot = new DisplayInventoryReader(api).Read();
        Assert.Equal(-1920, snapshot.Outputs[0].X); Assert.Equal(-100, snapshot.Outputs[0].Y);
        Assert.Equal(2u, snapshot.Outputs[0].SourceId); Assert.Equal(9u, snapshot.Outputs[0].Display.TargetId);
        var modes = snapshot.Page(snapshot.Outputs[0].Display, 0).Modes; Assert.Equal(3, modes.Length);
        Assert.Equal(new Rational(60000, 1001), modes[0].Mode.RefreshRate); Assert.Equal(new Rational(60, 1), modes[1].Mode.RefreshRate);
        Assert.Contains(modes, m => m.Mode.Width == 1280 && m.Mode.Height == 960);
        var plan = DisplayModePlanner.Draft(snapshot, snapshot.Id, snapshot.Outputs[0].Display, "driver-1", ScalingIntent.Stretched);
        Assert.Equal(snapshot.Outputs[1], Assert.Single(plan.PreservedOutputs)); Assert.Contains("game FOV", plan.Explanation);
        Assert.Equal(2, api.Queries);
    }
    [Fact] public void BufferRacesRetryWholeQueryWithinThreeAttempts()
    {
        var api = new Api(); api.Statuses.Enqueue(122); api.Statuses.Enqueue(122);
        Assert.NotEmpty(new DisplayInventoryReader(api).Read().Outputs); Assert.Equal(4, api.Queries);
        var bad = new Api(); for (var i = 0; i < 3; i++) bad.Statuses.Enqueue(122);
        Assert.Throws<InvalidOperationException>(() => new DisplayInventoryReader(bad).Read()); Assert.Equal(3, bad.Queries); Assert.Equal(0, bad.Enumerations);
    }
    [Theory] [InlineData(0)] [InlineData(129)]
    public void InvalidNativeCountsFailBeforeBufferAllocation(uint count)
    {
        var api = new Api { PathCount = count }; Assert.Throws<InvalidOperationException>(() => new DisplayInventoryReader(api).Read()); Assert.Equal(0, api.Queries);
    }
    [Fact] public void TopologyChangeAndAmbiguousIdentityFailClosed()
    {
        Assert.Throws<InvalidOperationException>(() => new DisplayInventoryReader(new Api { Changed = true }).Read());
        var api = new Api(); api.Outputs = [Output(), Output()]; Assert.Throws<InvalidOperationException>(() => new DisplayInventoryReader(api).Read());
    }
    [Fact] public void ClonesAreVisibleButCannotBePlannedAndStaleRuntimeMappingIsRefused()
    {
        var api = new Api(); api.Outputs = [Output(), Output(10)]; var inventory = new DisplayInventoryReader(api).Read();
        Assert.All(inventory.Outputs, o => Assert.True(o.Cloned));
        Assert.Throws<InvalidOperationException>(() => DisplayModePlanner.Draft(inventory, inventory.Id, inventory.Outputs[0].Display, "current", ScalingIntent.Unchanged));
        Assert.Throws<ArgumentException>(() => DisplayModePlanner.Draft(inventory, Guid.NewGuid(), inventory.Outputs[0].Display, "current", ScalingIntent.Unchanged));
        Assert.Throws<ArgumentException>(() => inventory.Page(inventory.Outputs[0].Display with { TargetId = 88 }, 0));
    }
    [Fact] public void ModernHdrAndWcgStayDistinctAndAbiCheckCallsNoHardware()
    {
        WindowsDisplayInventoryApi.VerifyAbi();
        Assert.Equal("SDR", WindowsDisplayInventoryApi.DescribeModernColor(69, 0).Mode);
        Assert.Equal("WCG", WindowsDisplayInventoryApi.DescribeModernColor(66, 1).Mode);
        Assert.Equal("HDR", WindowsDisplayInventoryApi.DescribeModernColor(50, 2).Mode);
        Assert.Contains("policy-limited=True", WindowsDisplayInventoryApi.DescribeModernColor(24, 0).Status);
        Assert.Equal("Unknown", WindowsDisplayInventoryApi.DescribeModernColor(0, 9).Mode);
    }
    [Fact] public async Task ReadOnlyQueryDoesNotChangeRevisionOrObservedColorAndRejectsExpiredSnapshot()
    {
        using var worker = new AgentStaWorker(); var initial = new EffectsState { Enabled = true, Held = true, Revision = 9, Generation = 12 };
        var coordinator = new JournaledEffectsCoordinator(worker, initial, readDisplays: () => new DisplayInventoryReader(new Api()).Read());
        Task<Chromatic.Core.Abstractions.EffectsAcknowledgment> Send(QueryDisplays query) => coordinator.SendAsync(new(Guid.NewGuid(), 0, 0, query));
        var ack = await Send(new()); Assert.True(ack.Completion.Success); Assert.Same(initial, coordinator.State);
        var next = await Send(new(ack.Inventory!.InventoryId, ack.Inventory.Outputs[0].Display)); Assert.True(next.Completion.Success);
        coordinator.InvalidateDisplayInventory();
        Assert.False((await Send(new(ack.Inventory.InventoryId, ack.Inventory.Outputs[0].Display))).Completion.Success);
        var refreshed = await Send(new()); Assert.NotEqual(ack.Inventory.InventoryId, refreshed.Inventory!.InventoryId);
        Assert.False((await Send(new(ack.Inventory.InventoryId, ack.Inventory.Outputs[0].Display))).Completion.Success);
        Assert.True((await Send(new(refreshed.Inventory.InventoryId, refreshed.Inventory.Outputs[0].Display))).Completion.Success);
        Assert.Same(initial, coordinator.State);
    }
    [Fact] public async Task UnsupportedModePreviewDoesNotLatchActivePreviewOrDisableWorkingColors()
    {
        using var worker = new AgentStaWorker(); var initial = new EffectsState { Enabled = true };
        var coordinator = new JournaledEffectsCoordinator(worker, initial);
        var ack = await coordinator.SendAsync(new(Guid.NewGuid(), 0, 0, new PreviewMode(Guid.NewGuid(), Output().Display, new(1280, 960, new(144, 1)), DateTimeOffset.UtcNow.AddSeconds(15))));
        Assert.Equal(CompletionStatus.NotImplemented, ack.Decision.Status); Assert.Same(initial, coordinator.State); Assert.False(coordinator.State.PreviewActive);
    }
    [Fact] public async Task InventoryTravelsThroughAuthenticatedPipeWithoutTouchingColorBackend()
    {
        var own = PipeInterop.Current; var peer = new VerifiedPipePeer(own.ProcessId, own.StartTicks, own.SessionId, own.UserSid, true);
        var launch = new PipeLaunchIdentity(PipeAuthorization.CreatePipeName(own.SessionId), PipeAuthorization.CreateNonce(), peer);
        using var server = PipeInterop.CreateCurrentUserLocalServer(launch.PipeName);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var accept = AuthenticatedPipe.AcceptAsync(server, launch, stop.Token);
        using var pipe = await AuthenticatedPipe.ConnectAsync(launch, stop.Token); await accept;
        using var worker = new AgentStaWorker();
        var coordinator = new JournaledEffectsCoordinator(worker, readDisplays: () => new DisplayInventoryReader(new Api()).Read());
        var serving = AuthenticatedPipe.ServeCommandsAsync(server, own.SessionId, coordinator, stop.Token);
        using var client = new EffectsPipeClient(pipe);
        try
        {
            var result = await client.SendAsync(new(Guid.NewGuid(), 0, 0, new QueryDisplays()), stop.Token);
            Assert.True(result.Completion.Success); Assert.Equal(2, result.Inventory!.Outputs.Length);
            Assert.Equal(0, client.State.Revision); Assert.False(client.State.Enabled);
        }
        finally
        {
            stop.Cancel();
            try { await serving; } catch (OperationCanceledException) { }
        }
    }
    [Fact] public void PagedIpcRoundTripIsBoundedAndRejectsUnknownFields()
    {
        var inventory = new DisplayInventoryReader(new Api()).Read(); var page = inventory.Page(inventory.Outputs[0].Display, 0);
        var response = new IpcResponse(1, "result", Guid.NewGuid(), new(CompletionStatus.Accepted, 0, 0, "Read"), new(true, "Fake", null, "Read", 0), IpcState.From(new()), page);
        var bytes = Protocol.Encode(response); Assert.True(bytes.Length < Protocol.MaxFrameBytes);
        Assert.Equal(page.InventoryId, Protocol.Decode<IpcResponse>(bytes).Inventory!.InventoryId);
        var command = new IpcRequest(1, "command", 1, new(Guid.NewGuid(), 0, 0, new QueryDisplays()));
        Assert.IsType<QueryDisplays>(Protocol.Decode<IpcRequest>(Protocol.Encode(command)).Envelope.Command);
        var invalid = System.Text.Encoding.UTF8.GetString(Protocol.Encode(command)).Replace("\"offset\": 0", "\"offset\": -1");
        Assert.Throws<Chromatic.Infrastructure.Persistence.ContractException>(() => Protocol.Decode<IpcRequest>(System.Text.Encoding.UTF8.GetBytes(invalid)));
    }
}
