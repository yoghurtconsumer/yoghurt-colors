using System.Diagnostics;
using System.IO;
using Xunit;

namespace Chromatic.Ui.Tests;

public sealed class LauncherProcessTests
{
    [Fact]
    public async Task ExactCmdLaunchesRenderedWindowFromAnotherDirectoryWithSpaces()
    {
        using var fixture = new LauncherFixture(includeApplication: true);
        var result = await fixture.RunAsync();
        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Contains("visible window and exited cleanly", result.Output);
    }

    [Fact]
    public async Task MissingExecutableProducesVisibleErrorAndNonzeroExit()
    {
        using var fixture = new LauncherFixture(includeApplication: false);
        var result = await fixture.RunAsync();
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("The app executable is missing", result.Output);
        Assert.Contains("Chromatic Desktop could not start", result.Output);
        Assert.Contains("Launch log:", result.Output);
    }

    private sealed class LauncherFixture : IDisposable
    {
        private readonly string _directory;
        private readonly string _foreignDirectory;
        public LauncherFixture(bool includeApplication)
        {
            var root = FindRepository();
            _directory = Path.Combine(root, "artifacts", "launcher tests", Guid.NewGuid().ToString("N"), "repo with spaces");
            _foreignDirectory = Path.Combine(_directory, "unrelated working directory");
            Directory.CreateDirectory(_foreignDirectory);
            Directory.CreateDirectory(Path.Combine(_directory, "eng"));
            File.Copy(Path.Combine(root, "Run-Chromatic.cmd"), Path.Combine(_directory, "Run-Chromatic.cmd"));
            foreach (var file in new[] { "Run-App.ps1", "Use-DotNet.ps1" })
                File.Copy(Path.Combine(root, "eng", file), Path.Combine(_directory, "eng", file));
            if (!includeApplication) return;

            var bin = new DirectoryInfo(AppContext.BaseDirectory);
            while (bin.Name != "bin") bin = bin.Parent ?? throw new InvalidOperationException("Test binary output not found.");
            var suffix = Path.GetRelativePath(bin.FullName, AppContext.BaseDirectory);
            var applicationOutput = Path.Combine(root, "src", "Chromatic.App", "bin", suffix);
            Assert.True(File.Exists(Path.Combine(applicationOutput, "Chromatic.App.exe")), "Build the solution before launcher tests.");
            var destination = Path.Combine(_directory, "src", "Chromatic.App", "bin", "x64", "Release", "net10.0-windows10.0.19041.0", "win-x64");
            foreach (var file in Directory.EnumerateFiles(applicationOutput, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(destination, Path.GetRelativePath(applicationOutput, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
        }
        public async Task<(int ExitCode, string Output)> RunAsync()
        {
            var batchPath = Path.Combine(_directory, "Run-Chromatic.cmd");
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe")
            {
                Arguments = $"/d /c \"\"{batchPath}\" -NoBuild -SmokeTest\"",
                WorkingDirectory = _foreignDirectory,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.Environment["CHROMATIC_LAUNCHER_NO_PAUSE"] = "1";
            // Reproduce the unmodified machine SDK preceding an already-present local SDK.
            start.Environment["PATH"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet")
                + ";" + Environment.GetEnvironmentVariable("PATH");
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start cmd launcher.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(50));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw new TimeoutException("Launcher hung instead of reporting its outcome."); }
            return (process.ExitCode, await output + await error);
        }
        private static string FindRepository()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(directory.FullName, "ChromaticDesktop.sln")))
                directory = directory.Parent ?? throw new InvalidOperationException("Repository not found.");
            return directory.FullName;
        }
        public void Dispose() => Directory.Delete(Path.GetDirectoryName(_directory)!, recursive: true);
    }
}
