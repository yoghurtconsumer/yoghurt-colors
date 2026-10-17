using Chromatic.Core.Models;
using Chromatic.Core.Validation;
namespace Chromatic.Infrastructure.Persistence;

public sealed class AutomationRepository(string path, IWriteFaultInjector? faults = null)
{
    private readonly AtomicJsonStore<AutomationSettings> _store = new(path, "automation", AutomationValidator.Validate, () => new(), faults);
    public Task<LoadResult<AutomationSettings>> LoadAsync(CancellationToken token = default) => _store.LoadAsync(token);
    public Task SaveAsync(AutomationSettings settings, CancellationToken token = default) => _store.SaveAsync(settings, token);
}
