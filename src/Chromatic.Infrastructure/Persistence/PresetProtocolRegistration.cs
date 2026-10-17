using Microsoft.Win32;

namespace Chromatic.Infrastructure.Persistence;

// No registry write occurs on construction, load or startup. The UI exposes these
// methods only through explicit registration buttons. Tests inject a memory store.
public interface IProtocolRegistry
{
    string? ReadCommand();
    void Write(string command);
    void Delete();
}
public sealed class UserProtocolRegistry : IProtocolRegistry
{
    private const string Root = @"Software\Classes\chromaticdesk";
    public string? ReadCommand()
    {
        using var root = Registry.CurrentUser.OpenSubKey(Root);
        if (root is null) return null;
        using var key = root.OpenSubKey(@"shell\open\command");
        return key?.GetValue("") as string ?? ""; // Existing incomplete ownership is not permission to overwrite it.
    }
    public void Write(string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Root);
        key.SetValue("", "URL:Chromatic Desktop preset"); key.SetValue("URL Protocol", "");
        using var icon = key.CreateSubKey("DefaultIcon"); icon.SetValue("", "\"" + Environment.ProcessPath + "\",0");
        using var open = key.CreateSubKey(@"shell\open\command"); open.SetValue("", command);
    }
    public void Delete() => Registry.CurrentUser.DeleteSubKeyTree(Root, false);
}
public sealed class PresetProtocolRegistration(string image, IProtocolRegistry registry)
{
    public string Command { get; } = BuildCommand(image);
    public string Status => registry.ReadCommand() switch { null => "Not registered.", var value when value == Command => "Registered to this app location.", _ => "Owned by another app/location; registration will not overwrite it." };
    private static string BuildCommand(string image)
    {
        var path = Path.GetFullPath(image);
        if (path.Any(c => c is '"' or '\r' or '\n') || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Expected an absolute application executable.");
        return "\"" + path + "\" \"%1\"";
    }
    public void Register()
    {
        var old = registry.ReadCommand();
        if (old is not null && old != Command) throw new UnauthorizedAccessException("Another application owns chromaticdesk. Remove that registration yourself first.");
        registry.Write(Command);
    }
    public void Unregister()
    {
        if (registry.ReadCommand() != Command) throw new UnauthorizedAccessException("This app does not own the registered command.");
        registry.Delete();
    }
}
