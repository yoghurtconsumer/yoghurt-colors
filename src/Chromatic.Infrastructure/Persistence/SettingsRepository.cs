using Chromatic.Core.Models;
using Chromatic.Core.Validation;

namespace Chromatic.Infrastructure.Persistence;
public sealed class SettingsRepository(string path)
{
    private readonly AtomicJsonStore<AppSettings> _store = new(path, "settings", SettingsValidator.Validate, () => new());
    public Task<LoadResult<AppSettings>> LoadAsync(CancellationToken cancellationToken = default) => _store.LoadAsync(cancellationToken);
    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) => _store.SaveAsync(settings, cancellationToken);
}
