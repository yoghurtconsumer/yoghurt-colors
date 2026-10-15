using System.Security.Cryptography;
using System.Text;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.Validation;

namespace Chromatic.Infrastructure.Persistence;
public sealed class ProfileRepository(string directory) : IProfileRepository
{
    private string PathFor(string id)
    {
        SettingsValidator.Text(id, 128, "profile ID");
        return Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))).ToLowerInvariant() + ".json");
    }
    public async Task<DisplayProfile?> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        var path = PathFor(id);
        if (!File.Exists(path) && !File.Exists(path + ".last-good")) return null;
        var value = (await Store(path).LoadAsync(cancellationToken)).Value;
        if (value.Id != id) throw new ContractException("Stored profile ID does not match lookup.");
        return value;
    }
    public Task SaveAsync(DisplayProfile profile, CancellationToken cancellationToken = default) =>
        Store(PathFor(profile.Id)).SaveAsync(profile, cancellationToken);
    private static AtomicJsonStore<DisplayProfile> Store(string path) => new(path, "profile", SettingsValidator.ValidateProfile, () => new());
}
