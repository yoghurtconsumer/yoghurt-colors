using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Chromatic.Native;

namespace Chromatic.App;

// Metadata only: never constructs a color adapter, initializes Magnification,
// launches a companion or calls a display setter.
public static class ReadOnlyDiagnostics
{
    public static string Save(Func<DisplaySafetyReport>? inventory = null)
    {
        var displays = (inventory ?? DisplaySafetyProbe.Read)();
        var report = new
        {
            mode = "ReadOnly", timestampUtc = DateTimeOffset.UtcNow, processId = Environment.ProcessId,
            executable = Environment.ProcessPath, framework = RuntimeInformation.FrameworkDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(), os = Environment.OSVersion.VersionString,
            minimumOsMet = RuntimeCapabilities.MeetsMinimum, displaySafety = displays,
            companions = new[] { "Agent", "Recovery" }.Select(role =>
            {
                var image = Path.Combine(AppContext.BaseDirectory, "companions", role, $"Chromatic.{role}.exe");
                return new { role, image, exists = File.Exists(image),
                    runtimeConfigExists = File.Exists(Path.ChangeExtension(image, ".runtimeconfig.json")),
                    assemblyExists = File.Exists(Path.ChangeExtension(image, ".dll")) };
            }).ToArray(),
            hardwareWritesPerformed = false
        };
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChromaticDesktop", "logs", "read-only-diagnostics.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Environment.ProcessId + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
        return path;
    }
}
