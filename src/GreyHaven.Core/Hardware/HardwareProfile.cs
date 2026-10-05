namespace GreyHaven.Core.Hardware;

/// <summary>The result of a scan: everything later layers (lookup, rating, reports) need to know about the PC.</summary>
public sealed record HardwareProfile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string GeneratedBy { get; init; }
    public required DateTimeOffset ScannedAt { get; init; }

    /// <summary>"live-scan", "sample:&lt;name&gt;" or "raw:&lt;file&gt;".</summary>
    public required string Source { get; init; }

    /// <summary>Human description, set for sample profiles.</summary>
    public string? Description { get; init; }

    public required SystemInfo System { get; init; }
    public required IReadOnlyList<HardwareComponent> Components { get; init; }

    /// <summary>Things the scan could not determine, in plain English.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
