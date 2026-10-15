using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Chromatic.Agent;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Ipc;
using Chromatic.Infrastructure.Persistence;
using Chromatic.Native;
using Chromatic.Recovery;

namespace Chromatic.ProcessHarness;

// Test-only executable. Every resource is a JSON file under the caller's fixture.
// Neither production executable references this project or can select these fakes.
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length != 2) return 64;
            var mode = args[0]; var directory = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(directory);
            var runtime = new CompanionRuntime(directory, () => new([], true, false, "Injected test SDR inventory."),
                (_, _, _) => new FileCatalog(directory));
            if (mode == "agent") return await AgentHost.RunAsync(await CompanionLaunch.ReadAsync(), runtime);
            if (mode == "recovery") return await RecoveryHost.RunAsync(await CompanionLaunch.ReadAsync(), runtime);
            FileResource.Save(Path.Combine(directory, "state.json"), Value(117));
            var ui = PipeInterop.Current;
            using var agent = StartChild(CompanionLaunch.Image("Agent"), "agent", directory);
            using var recovery = StartChild(CompanionLaunch.Image("Recovery"), "recovery", directory);
            var bootstrap = new CompanionBootstrap(1, ui, Environment.ProcessPath!, PipeInterop.ReadProcess(agent.Id), PipeInterop.ReadProcess(recovery.Id),
                PipeAuthorization.CreatePipeName(ui.SessionId), PipeAuthorization.CreatePipeName(ui.SessionId), PipeAuthorization.CreateNonce(), new());
            File.WriteAllText(Path.Combine(directory, "children.json"), JsonSerializer.Serialize(new[] { bootstrap.Agent, bootstrap.Recovery }));
            await CompanionLaunch.SendAsync(recovery, bootstrap); await CompanionLaunch.SendAsync(agent, bootstrap);
            using var pipe = await AuthenticatedPipe.ConnectAsync(new(bootstrap.AgentPipe, bootstrap.Nonce, CompanionLaunch.Peer(bootstrap.Agent)), default);
            using var client = new EffectsPipeClient(pipe);
            await client.QueryAsync();
            async Task Send(EffectsCommand command)
            {
                var ack = await client.SendAsync(new(Guid.NewGuid(), client.State.Revision, client.State.Generation, command));
                if (!ack.Completion.Success) throw new IOException($"{ack.Completion.ErrorCode}: {ack.Completion.Explanation}");
            }
            await Send(new SetColor(new(SaturationPercent: 180)));
            if (mode is "crash" or "hang") File.WriteAllText(Path.Combine(directory, mode), "1");
            try { await Send(new Enable(true)); }
            catch (Exception) when (mode is "crash" or "hang") { }
            if (mode == "ui-death")
            {
                File.WriteAllText(Path.Combine(directory, "ready"), "1");
                await Task.Delay(Timeout.Infinite);
            }
            if (mode == "foreign")
            {
                FileResource.Save(Path.Combine(directory, "state.json"), Value(230));
                // This is our fixture child, never a discovered machine process.
                agent.Kill();
            }
            if (mode == "graceful") await Send(new Shutdown());
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await agent.WaitForExitAsync(deadline.Token); await recovery.WaitForExitAsync(deadline.Token);
            File.WriteAllText(Path.Combine(directory, "finished"), $"{agent.ExitCode},{recovery.ExitCode}");
            return mode == "foreign" ? (recovery.ExitCode == 3 ? 0 : 4) : recovery.ExitCode;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error); return 99;
        }
    }
    private static Process StartChild(string image, string role, string directory)
    {
        var start = new ProcessStartInfo(image) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true };
        start.ArgumentList.Add(role); start.ArgumentList.Add(directory);
        return Process.Start(start) ?? throw new IOException("Fake child launch failed.");
    }
    internal static ResourceValue Value(double percent) => new(ColorMatrixBuilder.Saturation(percent).Values);
    private sealed class FileCatalog : IColorResourceCatalog
    {
        public IResourceAdapter Matrix { get; }
        public IReadOnlyList<IResourceAdapter> Resources { get; }
        public FileCatalog(string directory) { Matrix = new FileResource(directory); Resources = [Matrix]; }
        public void Dispose() { }
    }
    private sealed class FileResource(string directory) : IResourceAdapter
    {
        private readonly string _path = Path.Combine(directory, "state.json");
        public ResourceDescriptor Descriptor { get; } = new(OwnedResourceKind.GlobalMatrix, EffectScope.GlobalDesktop);
        public ResourceValue Canonicalize(ResourceValue value) => value;
        public bool CanOwn(ResourceValue baseline) => true;
        public Task<ResourceValue> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(
            JsonSerializer.Deserialize<ResourceValue>(File.ReadAllText(_path), JsonContract.Options)!);
        public Task WriteAsync(ResourceValue value, CancellationToken cancellationToken)
        {
            Save(_path, value);
            if (Path.GetFileName(Environment.ProcessPath) == "Chromatic.Agent.exe" && !value.Values.SequenceEqual(Value(117).Values))
            {
                if (File.Exists(Path.Combine(directory, "crash"))) Environment.Exit(71);
                if (File.Exists(Path.Combine(directory, "hang"))) Thread.Sleep(Timeout.Infinite);
            }
            return Task.CompletedTask;
        }
        public static void Save(string path, ResourceValue value)
        {
            var temp = path + "." + Environment.ProcessId + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(value, JsonContract.Options)); File.Move(temp, path, true);
        }
    }
}
