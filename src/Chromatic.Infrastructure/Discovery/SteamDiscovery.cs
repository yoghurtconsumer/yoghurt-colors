using System.Text;
using Microsoft.Win32;
namespace Chromatic.Infrastructure.Discovery;

public sealed record InstalledGame(string Name, string InstallDirectory, string AppId);
public static class SteamDiscovery
{
    public static string? UserSteamPath() => Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
    public static string NormalizeExecutable(string path)
    {
        if (path.Length is < 4 or > 32768 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] is not ('\\' or '/') ||
            path[2..].Contains(':') || path.Any(c => c < 32 || c is '"' or '<' or '>' or '|' or '*' or '?') || !Path.IsPathFullyQualified(path))
            throw new ArgumentException("Select a local absolute executable path.");
        var normalized = Path.GetFullPath(path).Replace('/', '\\');
        if (!normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Select an .exe file.");
        return normalized;
    }
    public static IReadOnlyList<InstalledGame> Discover(string steamPath)
    {
        static bool LocalDirectory(string path) => path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' &&
            path[2] is '\\' or '/' && !path[2..].Contains(':') && Path.IsPathFullyQualified(path);
        if (!LocalDirectory(steamPath))
            throw new ArgumentException("Steam discovery requires a local absolute directory.");
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(steamPath) };
        var libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFile))
        {
            var folders = Vdf.Parse(ReadBounded(libraryFile)).Children.SingleOrDefault(n => n.Key.Equals("libraryfolders", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("Missing Steam libraryfolders object.");
            foreach (var node in folders.Children.Where(n => uint.TryParse(n.Key, out _)))
            {
                var path = node.Value ?? node.Children.SingleOrDefault(n => n.Key.Equals("path", StringComparison.OrdinalIgnoreCase))?.Value;
                if (path is not null && LocalDirectory(path)) libraries.Add(Path.GetFullPath(path));
            }
        }
        if (libraries.Count > 32) throw new InvalidDataException("Too many Steam libraries.");
        var games = new List<InstalledGame>();
        var scanned = 0;
        foreach (var library in libraries)
        {
            var apps = Path.Combine(library, "steamapps"); if (!Directory.Exists(apps)) continue;
            foreach (var file in Directory.EnumerateFiles(apps, "appmanifest_*.acf").Take(513))
            {
                if (++scanned > 512) throw new InvalidDataException("Steam discovery limit exceeded.");
                try
                {
                    var app = Vdf.Parse(ReadBounded(file)).Children.SingleOrDefault(n => n.Key.Equals("AppState", StringComparison.OrdinalIgnoreCase));
                    if (app is null) continue;
                    var values = app.Children.Where(n => n.Value is not null).ToDictionary(n => n.Key, n => n.Value!, StringComparer.OrdinalIgnoreCase);
                    if (!values.TryGetValue("appid", out var id) || !uint.TryParse(id, out _) || !values.TryGetValue("name", out var name) ||
                        !values.TryGetValue("installdir", out var folder) || Path.IsPathRooted(folder) || folder.Split('\\', '/').Any(p => p is "." or "..")) continue;
                    var common = Path.GetFullPath(Path.Combine(apps, "common")) + Path.DirectorySeparatorChar;
                    var target = Path.GetFullPath(Path.Combine(common, folder));
                    if (name.Length > 128 || !target.StartsWith(common, StringComparison.OrdinalIgnoreCase)) continue;
                    games.Add(new(name, target, id)); // Never scan/launch game executables.
                }
                catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException) { }
            }
        }
        return games.DistinctBy(g => g.AppId).ToArray();
    }
    private static string ReadBounded(string path)
    {
        using var stream = File.OpenRead(path); if (stream.Length > 1048576) throw new InvalidDataException("VDF exceeds 1 MiB.");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes); return new UTF8Encoding(false, true).GetString(bytes);
    }
}
public sealed record Vdf(string Key, string? Value, IReadOnlyList<Vdf> Children)
{
    public IEnumerable<Vdf> Descendants() => Children.SelectMany(n => new[] { n }.Concat(n.Descendants()));
    public static Vdf Parse(string text)
    {
        if (text.Length > 1048576) throw new InvalidDataException("VDF too large.");
        int position = 0, tokens = 0;
        string? Token()
        {
            while (position < text.Length)
            {
                if (char.IsWhiteSpace(text[position])) { position++; continue; }
                if (position + 1 < text.Length && text[position..(position + 2)] == "//") { while (position < text.Length && text[position] != '\n') position++; continue; }
                break;
            }
            if (position == text.Length) return null;
            if (++tokens > 32768) throw new InvalidDataException("VDF token limit.");
            var c = text[position++]; if (c is '{' or '}') return c.ToString();
            if (c != '"') throw new InvalidDataException("Expected quoted VDF token.");
            var value = new StringBuilder();
            while (position < text.Length)
            {
                c = text[position++]; if (c == '"') return value.ToString();
                if (c == '\\' && position < text.Length && text[position] is '\\' or '"') c = text[position++];
                value.Append(c); if (value.Length > 32768) throw new InvalidDataException("VDF token too long.");
            }
            throw new InvalidDataException("Unterminated VDF token.");
        }
        List<Vdf> Read(int depth, bool nested)
        {
            if (depth > 16) throw new InvalidDataException("VDF depth exceeded.");
            var nodes = new List<Vdf>(); var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (Token() is { } key)
            {
                if (key == "}" && nested) return nodes;
                if (key is "{" or "}" || !keys.Add(key)) throw new InvalidDataException("Invalid/duplicate VDF key.");
                var value = Token() ?? throw new InvalidDataException("Missing VDF value.");
                nodes.Add(value == "{" ? new(key, null, Read(depth + 1, true)) : value == "}" ? throw new InvalidDataException("Invalid VDF value.") : new(key, value, []));
            }
            if (nested) throw new InvalidDataException("Missing VDF closing brace."); return nodes;
        }
        return new("root", null, Read(0, false));
    }
}
