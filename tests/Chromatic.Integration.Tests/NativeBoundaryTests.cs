using System.Reflection;
using System.Runtime.InteropServices;
using Chromatic.Agent;
using Chromatic.Core.State;
using Chromatic.Native;
using Xunit;

namespace Chromatic.Integration.Tests;

public sealed class NativeBoundaryTests
{
    // Expanding native access must update this reviewed boundary. Reflection does
    // not execute any import and includes CsWin32-generated imports in the audit.
    [Fact] public void CompiledNativeImportsStayWithinDisplayAndCompanionBoundary()
    {
        Dictionary<string, HashSet<string>> allowed = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Magnification.dll"] = ["MagInitialize", "MagUninitialize", "MagGetFullscreenColorEffect", "MagSetFullscreenColorEffect"],
            ["gdi32.dll"] = ["CreateDCW", "DeleteDC", "GetDeviceGammaRamp", "SetDeviceGammaRamp"],
            ["user32.dll"] = ["GetDisplayConfigBufferSizes", "QueryDisplayConfig", "DisplayConfigGetDeviceInfo"],
            ["kernel32.dll"] = ["LoadLibraryExW", "GetProcAddress", "FreeLibrary", "CreateNamedPipeW", "LocalFree",
                "GetNamedPipeClientProcessId", "GetNamedPipeServerProcessId", "OpenProcess", "GetProcessTimes",
                "QueryFullProcessImageNameW", "WaitForSingleObject", "TerminateProcess"],
            ["advapi32.dll"] = ["ConvertStringSecurityDescriptorToSecurityDescriptorW", "OpenProcessToken", "GetTokenInformation"],
            ["wtsapi32.dll"] = ["WTSRegisterSessionNotification", "WTSUnRegisterSessionNotification"]
        };
        var imports = new[] { typeof(AbiAssertions).Assembly, typeof(AgentHost).Assembly }
            .SelectMany(a => a.GetTypes()).SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Select(m => m.GetCustomAttribute<DllImportAttribute>()).OfType<DllImportAttribute>().ToArray();
        Assert.NotEmpty(imports);
        foreach (var import in imports)
            Assert.True(allowed.TryGetValue(import.Value, out var entries) && entries.Contains(import.EntryPoint!),
                $"Unreviewed native entry point: {import.Value}!{import.EntryPoint}");
    }
    [Fact] public void CoreCannotReferenceNativeOrDesktopAssemblies()
    {
        var references = typeof(EffectsReducer).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.DoesNotContain(references, n => n is not null && (n.StartsWith("Chromatic.") || n is "PresentationFramework" or "WindowsBase" or "System.Windows.Forms"));
        Assert.DoesNotContain(typeof(EffectsReducer).Assembly.GetTypes().SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)),
            m => m.GetCustomAttribute<DllImportAttribute>() is not null);
    }
}
