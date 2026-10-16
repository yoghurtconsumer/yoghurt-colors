using System.Diagnostics;
using System.Text.Json;
using Chromatic.Core.Models;
using Chromatic.Infrastructure.Persistence;
using Chromatic.Native;

namespace Chromatic.Infrastructure.Ipc;

public sealed record CompanionBootstrap(int Version, NativePeerIdentity Ui, string UiImage,
    NativePeerIdentity Agent, NativePeerIdentity Recovery, string AgentPipe, string RecoveryPipe, string Nonce,
    OptionalServices Services, bool RecoveryOnly = false);

public static class CompanionLaunch
{
    // Startup/package verification only: no bootstrap, pipes, providers or writes.
    public static int StartupSmoke(string role)
    {
        var supported = RuntimeCapabilities.MeetsMinimum;
        Console.WriteLine(JsonSerializer.Serialize(new { role, supported, hardwareWritesPerformed = false,
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            coreLibrary = typeof(object).Assembly.Location }));
        return supported ? 0 : 1;
    }
    public static string SessionDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ChromaticDesktop", "sessions", PipeInterop.Current.SessionId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public static VerifiedPipePeer Peer(NativePeerIdentity identity) => new(identity.ProcessId, identity.StartTicks, identity.SessionId, identity.UserSid, true);
    public static string Image(string role) => role is "Agent" or "Recovery"
        ? Path.Combine(AppContext.BaseDirectory, "companions", role, $"Chromatic.{role}.exe")
        : throw new ArgumentException("Unknown companion role.");
    public static Process Start(string image)
    {
        if (!File.Exists(image)) throw new FileNotFoundException("Build the application with companion outputs.", image);
        return Process.Start(new ProcessStartInfo(image)
        {
            WorkingDirectory = Path.GetDirectoryName(image)!, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardInput = true
        }) ?? throw new IOException("Companion launch failed.");
    }
    public static async Task SendAsync(Process process, CompanionBootstrap bootstrap)
    {
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(bootstrap, new JsonSerializerOptions(JsonContract.Options) { WriteIndented = false }));
        process.StandardInput.Close();
    }
    public static async Task<CompanionBootstrap> ReadAsync()
    {
        if (!Console.IsInputRedirected) throw new UnauthorizedAccessException("Companions require a private inherited bootstrap pipe from the application.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        // Bounded, one-shot inherited stdin; launch secrets never appear in argv or disk.
        var chars = new char[16384]; var count = 0;
        while (count < chars.Length)
        {
            var n = await Console.In.ReadAsync(chars.AsMemory(count, 1), timeout.Token);
            if (n == 0 || chars[count] == '\n') break;
            count++;
        }
        if (count == chars.Length) throw new ContractException("Oversized bootstrap.");
        var value = JsonSerializer.Deserialize<CompanionBootstrap>(new string(chars, 0, count), JsonContract.Options)
            ?? throw new ContractException("Empty bootstrap.");
        var current = PipeInterop.Current;
        if (value.Version != 1 || value.Nonce.Length != 64 || !value.Nonce.All(Uri.IsHexDigit) ||
            new[] { value.Ui, value.Agent, value.Recovery }.Any(p => p.UserSid != current.UserSid || p.SessionId != current.SessionId) ||
            (current != value.Agent && current != value.Recovery)) throw new UnauthorizedAccessException("Invalid companion bootstrap identity.");
        return value;
    }
    public static void Log(string role, string message)
    {
        try
        {
            Directory.CreateDirectory(SessionDirectory);
            var path = Path.Combine(SessionDirectory, role + ".log");
            if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) File.Move(path, path + ".previous", true);
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
