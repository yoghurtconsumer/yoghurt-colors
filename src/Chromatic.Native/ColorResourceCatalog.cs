using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;

namespace Chromatic.Native;

// Construction is inert. Native reads/writes are lazy and remain on the owning STA.
public interface IColorResourceCatalog : IDisposable
{
    IResourceAdapter Matrix { get; }
    IReadOnlyList<IResourceAdapter> Resources { get; }
}
public sealed class ColorResourceCatalog : IColorResourceCatalog
{
    public IResourceAdapter Matrix { get; }
    public IReadOnlyList<IResourceAdapter> Resources { get; }
    public ColorResourceCatalog(DisplaySafetyReport inventory, bool gamma, bool nvidia)
    {
        bool Sdr() => DisplaySafetyProbe.Read().AllSdr;
        bool Mapped(DisplayIdentity display) => DisplaySafetyProbe.Read().Contains(display);
        Matrix = new MagnificationResource(new MagnificationApi(), Sdr);
        var resources = new List<IResourceAdapter> { Matrix };
        foreach (var display in inventory.Displays)
        {
            if (gamma) resources.Add(new GammaResource(display, () => new GammaApi(display.GdiName), Mapped, Sdr));
            if (nvidia) resources.Add(new NvidiaDvcResource(display, () => new NvApi(), Mapped, Sdr));
        }
        Resources = resources.AsReadOnly();
    }
    public void Dispose()
    {
        foreach (var resource in Resources.OfType<IDisposable>().Reverse()) resource.Dispose();
    }
}
