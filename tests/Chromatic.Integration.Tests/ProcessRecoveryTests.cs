using System.Diagnostics;
using System.Text.Json;
using Chromatic.Core.Color;
using Chromatic.Core.Models;
using Chromatic.Infrastructure.Persistence;
using Chromatic.Infrastructure.Effects;
using Chromatic.Native;
using Xunit;

namespace Chromatic.Integration.Tests;

public sealed class ProcessRecoveryTests
{
    [Theory]
    [InlineData("graceful")]
    [InlineData("crash")]
    [InlineData("hang")]
    [InlineData("foreign")]
    [InlineData("ui-apply")]
    public async Task IndependentRecoveryUsesCapturedBaselineAfterVerifiedChildExit(string mode)
    {
        using var fixture = new Fixture();
        using var child = fixture.Start(mode);
        var output = child.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        await child.WaitForExitAsync(timeout.Token);
        Assert.True(child.ExitCode == 0, await output);
        var snapshot = await new RecoveryJournalStore(Path.Combine(fixture.StateDirectory, "recovery.json")).LoadAsync(default);
        Assert.NotNull(snapshot);
        Assert.Equal(mode == "foreign" ? JournalStatus.Conflict : JournalStatus.Restored, snapshot.Status);
        AssertValue(fixture.StateDirectory, mode == "foreign" ? 230 : 117);
        if (mode != "foreign") Assert.True(OwnedResourceController.Equivalent(snapshot.Resources[0].Baseline, ReadValue(fixture.StateDirectory)),
            "Recovery must preserve every captured baseline value exactly, including rounding.");
    }
    [Theory] [InlineData("mode-graceful")] [InlineData("mode-crash")] [InlineData("mode-hang")] [InlineData("mode-timeout")]
    public async Task IndependentProcessesRestorePreviewTopologyUsingOnlyFileAdapters(string mode)
    {
        using var fixture = new Fixture(); using var child = fixture.Start(mode);
        var error = child.StandardError.ReadToEndAsync(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        await child.WaitForExitAsync(timeout.Token); Assert.True(child.ExitCode == 0, await error);
        var value = JsonSerializer.Deserialize<ResourceValue>(File.ReadAllText(Path.Combine(fixture.StateDirectory, "mode.json")), JsonContract.Options)!;
        Assert.Equal("original complete fake topology", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value.TopologyBase64!)));
        var journal = await new RecoveryJournalStore(Path.Combine(fixture.StateDirectory, "display-preview.json")).LoadAsync(timeout.Token);
        Assert.Equal(JournalStatus.Restored, journal!.Status);
    }
    [Fact] public async Task UnexpectedUiDeathDuringPreviewRestoresTopologyBeforeRecoveryExits()
    {
        using var fixture = new Fixture(); using var ui = fixture.Start("mode-ui-death"); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!File.Exists(Path.Combine(fixture.StateDirectory, "ready"))) { Assert.False(ui.HasExited); await Task.Delay(30, timeout.Token); }
        var children = JsonSerializer.Deserialize<NativePeerIdentity[]>(await File.ReadAllTextAsync(Path.Combine(fixture.StateDirectory, "children.json"), timeout.Token))!;
        using var agent = new VerifiedProcess(children[0], Path.Combine(fixture.Root, "companions", "Agent", "Chromatic.Agent.exe"));
        using var recovery = new VerifiedProcess(children[1], Path.Combine(fixture.Root, "companions", "Recovery", "Chromatic.Recovery.exe"));
        ui.Kill(); await ui.WaitForExitAsync(timeout.Token); await agent.WaitForExitAsync(timeout.Token); await recovery.WaitForExitAsync(timeout.Token);
        var value = JsonSerializer.Deserialize<ResourceValue>(File.ReadAllText(Path.Combine(fixture.StateDirectory, "mode.json")), JsonContract.Options)!;
        Assert.Equal("original complete fake topology", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value.TopologyBase64!)));
        Assert.Equal(JournalStatus.Restored, (await new RecoveryJournalStore(Path.Combine(fixture.StateDirectory, "display-preview.json")).LoadAsync(timeout.Token))!.Status);
    }
    [Fact] public async Task UnexpectedUiDeathMakesAgentRestoreBeforeExiting()
    {
        using var fixture = new Fixture();
        using var ui = fixture.Start("ui-death");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!File.Exists(Path.Combine(fixture.StateDirectory, "ready")))
        {
            Assert.False(ui.HasExited, ui.HasExited ? await ui.StandardError.ReadToEndAsync() : "");
            await Task.Delay(50, timeout.Token);
        }
        var children = JsonSerializer.Deserialize<NativePeerIdentity[]>(await File.ReadAllTextAsync(Path.Combine(fixture.StateDirectory, "children.json"), timeout.Token))!;
        using var agent = new VerifiedProcess(children[0], Path.Combine(fixture.Root, "companions", "Agent", "Chromatic.Agent.exe"));
        using var recovery = new VerifiedProcess(children[1], Path.Combine(fixture.Root, "companions", "Recovery", "Chromatic.Recovery.exe"));
        ui.Kill(); // exact fixture-owned fake UI process, not a discovered application
        await ui.WaitForExitAsync(timeout.Token); await agent.WaitForExitAsync(timeout.Token); await recovery.WaitForExitAsync(timeout.Token);
        AssertValue(fixture.StateDirectory, 117);
        var journal = await new RecoveryJournalStore(Path.Combine(fixture.StateDirectory, "recovery.json")).LoadAsync(timeout.Token);
        Assert.Equal(JournalStatus.Restored, journal!.Status);
    }
    private static void AssertValue(string directory, double saturation)
    {
        var value = ReadValue(directory);
        // Independent child and test-host JITs can differ by one ULP in the
        // reference calculation. Exact restoration is checked against the journal above.
        Assert.Equal(25, value.Values.Length);
        foreach (var pair in ColorMatrixBuilder.Saturation(saturation).Values.Zip(value.Values)) Assert.Equal(pair.First, pair.Second, 12);
    }
    private static ResourceValue ReadValue(string directory) => JsonSerializer.Deserialize<ResourceValue>(
        File.ReadAllText(Path.Combine(directory, "state.json")), JsonContract.Options)!;
    private sealed class Fixture : IDisposable
    {
        public string Root { get; }
        public string StateDirectory => Path.Combine(Root, "fake state");
        private Process? _ui;
        public Fixture()
        {
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(repo.FullName, "ChromaticDesktop.sln"))) repo = repo.Parent!;
            var suffix = AppContext.BaseDirectory[(AppContext.BaseDirectory.IndexOf("bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal) + 4)..];
            var source = Path.Combine(repo.FullName, "tests", "Chromatic.ProcessHarness", "bin", suffix);
            Root = Path.Combine(repo.FullName, "artifacts", "process tests", Guid.NewGuid().ToString("N"));
            Assert.True(File.Exists(Path.Combine(source, "Chromatic.ProcessHarness.exe")), source);
            foreach (var target in new[] { Root, Path.Combine(Root, "companions", "Agent"), Path.Combine(Root, "companions", "Recovery") })
            {
                Directory.CreateDirectory(target);
                foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    var destination = Path.Combine(target, Path.GetRelativePath(source, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(file, destination, true);
                }
                if (target != Root) File.Copy(Path.Combine(source, "Chromatic.ProcessHarness.exe"),
                    Path.Combine(target, "Chromatic." + Path.GetFileName(target) + ".exe"), true);
            }
        }
        public Process Start(string mode)
        {
            var start = new ProcessStartInfo(Path.Combine(Root, "Chromatic.ProcessHarness.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            start.ArgumentList.Add(mode); start.ArgumentList.Add(StateDirectory);
            return _ui = Process.Start(start)!;
        }
        public void Dispose()
        {
            try { if (_ui is { HasExited: false }) _ui.Kill(); } catch (InvalidOperationException) { }
            // Keep diagnostics in ignored artifacts. Descendants observe this UI handle;
            // no recursive filesystem deletion or process-name kill is used.
        }
    }
}
