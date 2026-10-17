using Chromatic.Core.Models;
using Chromatic.Core.Abstractions;
using Chromatic.Native;

namespace Chromatic.Infrastructure.Ipc;

// Injection boundary for tests. Production executables always construct this default;
// no command-line, settings or public IPC option can select a fake adapter.
public sealed record CompanionRuntime(string SessionDirectory, Func<DisplaySafetyReport> Inventory,
    Func<DisplaySafetyReport, bool, bool, IColorResourceCatalog> CreateResources)
{
    public Func<IDisplayModeResource>? CreateDisplayMode { get; init; }
    public static CompanionRuntime Production => new(CompanionLaunch.SessionDirectory, DisplaySafetyProbe.Read,
        (inventory, gamma, nvidia) => new ColorResourceCatalog(inventory, gamma, nvidia))
        { CreateDisplayMode = () => new CcdDisplayModeResource(new WindowsCcdModeApi()) };
}
