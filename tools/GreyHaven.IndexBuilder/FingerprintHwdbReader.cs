using GreyHaven.Compat.Kernel;

namespace GreyHaven.IndexBuilder;

/// <summary>
/// Reads libfprint's <c>60-autosuspend-libfprint-2.hwdb</c>. Sections look like:
/// <code>
/// # Supported by libfprint driver goodixmoc
/// usb:v27C6p633C*
///  ID_AUTOSUSPEND=1
///
/// # Known unsupported devices
/// usb:v04F3p0C4C*
/// </code>
/// Targets are "libfprint:&lt;driver&gt;" or <see cref="Unsupported"/>.
/// </summary>
internal static class FingerprintHwdbReader
{
    public const string Unsupported = "libfprint:unsupported";
    private const string SupportedPrefix = "# Supported by libfprint driver ";

    public static IEnumerable<AliasEntry> Read(TextReader reader)
    {
        string? target = null;
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith(SupportedPrefix, StringComparison.Ordinal))
                target = "libfprint:" + line[SupportedPrefix.Length..].Trim();
            else if (line.StartsWith("# Known unsupported devices", StringComparison.Ordinal))
                target = Unsupported;
            else if (line.StartsWith("#", StringComparison.Ordinal))
                target = null; // any other comment block (e.g. license header) ends the section
            else if (target is not null && line.StartsWith("usb:v", StringComparison.Ordinal))
                yield return new AliasEntry(line.Trim(), target);
        }
    }
}
