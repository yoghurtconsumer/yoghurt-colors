using Chromatic.Core.Models;
using Chromatic.Core.Validation;
namespace Chromatic.Infrastructure.Persistence;
public sealed class PresetRepository(string path)
{
    private readonly AtomicJsonStore<PresetDocument> _store = new(path, "preset", PresetValidator.Validate, () => new());
    public Task<LoadResult<PresetDocument>> LoadAsync(CancellationToken cancellationToken = default) => _store.LoadAsync(cancellationToken);
    public Task SaveAsync(PresetDocument preset, CancellationToken cancellationToken = default) => _store.SaveAsync(preset, cancellationToken);
}
