namespace GreyHaven.Compat.Data;

/// <summary>Contents of <c>data/windows.yaml</c>.</summary>
public sealed class WindowsLifecycle
{
    public Windows10Lifecycle Windows10 { get; set; } = new();
}

public sealed class Windows10Lifecycle
{
    /// <summary>ISO date support ended, e.g. "2025-10-14".</summary>
    public string EndOfSupport { get; set; } = "";
    /// <summary>ISO date Extended Security Updates end.</summary>
    public string EsuEnds { get; set; } = "";
    public SourceLink Source { get; set; } = new();
    public string? Checked { get; set; }

    public DateOnly EndOfSupportDate => Parse(EndOfSupport, "endOfSupport");
    public DateOnly EsuEndsDate => Parse(EsuEnds, "esuEnds");

    private static DateOnly Parse(string value, string field) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", out var d) ? d : throw new InvalidDataException($"windows.yaml: windows10.{field} '{value}' must be a date like 2025-10-14.");
}
