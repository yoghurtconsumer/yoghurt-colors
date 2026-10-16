using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Chromatic.App;

public sealed class StartupDiagnostics
{
    public string ReportPath { get; }
    public StartupDiagnostics()
    {
        var suppliedId = Environment.GetEnvironmentVariable("CHROMATIC_LAUNCH_ID");
        var id = Guid.TryParseExact(suppliedId, "N", out var parsed) ? parsed : Guid.NewGuid();
        ReportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChromaticDesktop", "logs", $"startup-{id:N}.json");
    }
    public void Ready(bool windowVisible, bool smokeTest = false) => Write("Ready", windowVisible, "Main window rendered.", smokeTest);
    public void Failed(Exception error) => Write("Failed", false, error.ToString());
    private void Write(string status, bool windowVisible, string message, bool smokeTest = false)
    {
        // Best-effort diagnostics must not prevent startup or replace the original failure.
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
            var json = JsonSerializer.Serialize(new { status, processId = Environment.ProcessId, windowVisible,
                timestampUtc = DateTimeOffset.UtcNow, message, smokeTest, executable = Environment.ProcessPath,
                framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                coreLibrary = typeof(object).Assembly.Location });
            var temporary = ReportPath + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, ReportPath, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Trace.WriteLine($"Startup diagnostics unavailable: {error.GetType().Name}"); }
    }
}
