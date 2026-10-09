using System.Text.Json;
using System.Text.RegularExpressions;

namespace GreyHaven.Media;

/// <summary>The only things the elevated helper (GreyHaven.Helper.exe) will ever do. One per launch.</summary>
public enum HelperAction { FastStartupOff, FastStartupOn, BitLockerOff, WriteUsb }

/// <summary>
/// A request to the elevated helper, passed as command-line arguments. Parsed strictly: anything
/// unexpected is refused. The helper re-checks the disk itself (it never trusts the app's word),
/// and talks back over a named pipe that only this user can open, after sending a one-time token.
/// </summary>
public sealed partial record HelperRequest
{
    public required HelperAction Action { get; init; }
    public required string Pipe { get; init; }
    public required string Token { get; init; }
    // WriteUsb only:
    public int? Disk { get; init; }
    /// <summary>The model and size the person saw. The helper refuses if disk N is now something else.</summary>
    public string? Model { get; init; }
    public long? Size { get; init; }
    public string? Image { get; init; }
    public string? Sha256 { get; init; }
    /// <summary>The person confirmed erasing a drive bigger than a USB stick (see <see cref="StickCheck.IsLargeDrive"/>).</summary>
    public bool LargeDrive { get; init; }

    private static readonly Dictionary<string, HelperAction> Verbs = new(StringComparer.Ordinal)
    {
        ["fast-startup-off"] = HelperAction.FastStartupOff,
        ["fast-startup-on"] = HelperAction.FastStartupOn,
        ["bitlocker-off"] = HelperAction.BitLockerOff,
        ["write-usb"] = HelperAction.WriteUsb,
    };

    public IReadOnlyList<string> ToArgs()
    {
        var args = new List<string> { Verbs.First(v => v.Value == Action).Key, "--pipe", Pipe, "--token", Token };
        if (Action == HelperAction.WriteUsb)
            args.AddRange(["--disk", Disk!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), "--model", Model!, "--size",
                Size!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), "--image", Image!, "--sha256", Sha256!]);
        if (Action == HelperAction.WriteUsb && LargeDrive) args.AddRange(["--large-drive", "yes"]);
        return args;
    }

    /// <summary>Parses the helper's arguments, or throws <see cref="ArgumentException"/> with the reason.</summary>
    public static HelperRequest Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || !Verbs.TryGetValue(args[0], out var action)) throw new ArgumentException("Unknown action.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Count; i += 2)
        {
            if (i + 1 >= args.Count || !args[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Unexpected argument '{args[i]}'.");
            if (!values.TryAdd(args[i][2..], args[i + 1])) throw new ArgumentException($"'{args[i]}' given twice.");
        }
        string Need(string key) => values.TryGetValue(key, out var v) ? v : throw new ArgumentException($"Missing --{key}.");

        var allowed = action == HelperAction.WriteUsb ? new[] { "pipe", "token", "disk", "model", "size", "image", "sha256", "large-drive" } : ["pipe", "token"];
        if (values.Keys.FirstOrDefault(k => !allowed.Contains(k)) is { } extra) throw new ArgumentException($"Unexpected --{extra}.");

        var pipe = Need("pipe");
        if (!PipeName().IsMatch(pipe)) throw new ArgumentException("Bad pipe name.");
        var token = Need("token");
        if (!TokenPattern().IsMatch(token)) throw new ArgumentException("Bad token.");
        var request = new HelperRequest { Action = action, Pipe = pipe, Token = token };
        if (action != HelperAction.WriteUsb) return request;

        if (!int.TryParse(Need("disk"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var disk) || disk is < 0 or > 255)
            throw new ArgumentException("Bad disk number.");
        if (!long.TryParse(Need("size"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var size) || size <= 0)
            throw new ArgumentException("Bad size.");
        var image = Need("image");
        if (!Path.IsPathFullyQualified(image) || !image.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("The image must be a full path to an .iso file.");
        var sha = Need("sha256");
        if (sha.Length != 64 || !sha.All(Uri.IsHexDigit)) throw new ArgumentException("Bad SHA-256.");
        var large = values.TryGetValue("large-drive", out var l) ? l == "yes" ? true : throw new ArgumentException("Bad --large-drive.") : false;
        return request with { Disk = disk, Model = Need("model"), Size = size, Image = image, Sha256 = sha.ToLowerInvariant(), LargeDrive = large };
    }

    [GeneratedRegex(@"^GreyHaven-[0-9a-f]{32}$")] private static partial Regex PipeName();
    [GeneratedRegex(@"^[0-9a-f]{64}$")] private static partial Regex TokenPattern();

    public static string NewPipeName() => $"GreyHaven-{Guid.NewGuid():N}";
    public static string NewToken() => Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
}

/// <summary>One line of JSON from the helper: progress, done, or an error the person can read.</summary>
public sealed record HelperMessage(string Kind, string? Stage = null, long Done = 0, long? Total = null, string? Text = null)
{
    public const string Progress = "progress", Ok = "done", Error = "error", Refused = "refused";

    public string ToLine() => JsonSerializer.Serialize(this, Options);
    public static HelperMessage? FromLine(string line)
    {
        try { return JsonSerializer.Deserialize<HelperMessage>(line, Options); }
        catch (JsonException) { return null; }
    }

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

/// <summary>Plain words shown before Windows asks for permission, so the prompt is never a surprise.</summary>
public static class HelperReasons
{
    public static string Why(HelperAction a) => a switch
    {
        HelperAction.FastStartupOff or HelperAction.FastStartupOn => "Windows will ask for permission so GreyHaven can change the Fast Startup setting.",
        HelperAction.BitLockerOff => "Windows will ask for permission so GreyHaven can turn off drive encryption.",
        _ => "Windows will ask for permission so GreyHaven can write to your USB stick.",
    };
}
