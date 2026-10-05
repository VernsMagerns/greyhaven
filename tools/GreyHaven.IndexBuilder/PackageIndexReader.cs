using GreyHaven.Compat.Kernel;

namespace GreyHaven.IndexBuilder;

/// <summary>
/// Reads the <c>Modaliases:</c> field from a Debian package index. Format:
/// <c>Modaliases: nvidia(pci:v000010DEd00002484sv*sd*bc03sc*i*, pci:...), other(...)</c>.
/// The index target is the package name, since that's what the user installs.
/// </summary>
internal static class PackageIndexReader
{
    public static IEnumerable<AliasEntry> ReadModaliases(TextReader reader)
    {
        string? package = null;
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("Package: ", StringComparison.Ordinal))
            {
                package = line[9..].Trim();
            }
            else if (line.StartsWith("Modaliases: ", StringComparison.Ordinal) && package is not null)
            {
                foreach (var pattern in ParsePatterns(line[12..]))
                    yield return new AliasEntry(pattern, package);
            }
            else if (line.Length == 0)
            {
                package = null;
            }
        }
    }

    internal static IEnumerable<string> ParsePatterns(string field)
    {
        // Strip "module(" ... ")" groups and split on commas.
        var depth = 0;
        var current = new System.Text.StringBuilder();
        foreach (var ch in field)
        {
            if (ch == '(') { depth++; current.Clear(); continue; }
            if (ch == ')') { depth--; if (current.Length > 0) yield return current.ToString().Trim(); current.Clear(); continue; }
            if (depth == 0) continue;
            if (ch == ',') { if (current.Length > 0) yield return current.ToString().Trim(); current.Clear(); continue; }
            current.Append(ch);
        }
    }
}
