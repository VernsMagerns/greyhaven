using GreyHaven.Core.Compatibility;

namespace GreyHaven.Core.Hardware;

/// <summary>
/// Beginner-level wording (short sentences, about a 6th-grade reading level) shared by the app
/// and the saved report. Technical names stay one click away, behind "Show more".
/// </summary>
public static class PlainWords
{
    /// <summary>Short rating words for badges, e.g. "Needs one extra step".</summary>
    public static string Rating(Rating r) => r switch
    {
        Compatibility.Rating.WorksOutOfTheBox => "Works right away",
        Compatibility.Rating.WorksWithExtraDriver => "Needs one extra step",
        Compatibility.Rating.Partial => "Partly works",
        Compatibility.Rating.Unsupported => "Won't work",
        _ => "Not sure",
    };

    /// <summary>The one-line summary for a rating: the data's own line, or a default.</summary>
    public static string Summary(ComponentRating rating, string distroName) => rating.Summary ?? (rating.Rating switch
    {
        Compatibility.Rating.WorksOutOfTheBox => "Works right away.",
        Compatibility.Rating.WorksWithExtraDriver => $"Works after one extra step. It takes a few clicks once {distroName} is installed.",
        Compatibility.Rating.Partial => $"Only partly works on {distroName}.",
        Compatibility.Rating.Unsupported => $"Won't work on {distroName}.",
        _ => "We couldn't check this part. Try it from a USB stick first.",
    });

    /// <summary>A part as a noun in a sentence: "Your graphics card needs...".</summary>
    public static string Noun(ComponentCategory c) => c switch
    {
        ComponentCategory.Cpu => "processor",
        ComponentCategory.Gpu => "graphics card",
        ComponentCategory.StorageController => "storage",
        ComponentCategory.NvmeDrive => "SSD",
        ComponentCategory.Wifi => "Wi-Fi",
        ComponentCategory.Ethernet => "wired network",
        ComponentCategory.Bluetooth => "Bluetooth",
        ComponentCategory.AudioController or ComponentCategory.AudioCodec => "sound",
        ComponentCategory.DisplayAudio => "HDMI sound",
        ComponentCategory.Webcam => "camera",
        ComponentCategory.FingerprintReader => "fingerprint reader",
        _ => Labels.Of(c).ToLowerInvariant(),
    };

    /// <summary>Headline for the results, from the recommended distro's score.</summary>
    public static string Headline(int score) => score switch
    {
        >= 90 => "Your PC is ready for Linux",
        >= 70 => "Your PC should work well with Linux",
        >= 50 => "Linux will work, with a few problems",
        _ => "Linux may not work well on this PC",
    };

    /// <summary>
    /// "16 parts work right away. 1 part needs one extra step after you install." Parts nobody
    /// could check are only a worry when they're essential or important; small ones are mentioned calmly.
    /// </summary>
    public static string PartsSummary(IReadOnlyList<(Rating Rating, Importance Importance)> parts)
    {
        int N(Rating r) => parts.Count(x => x.Rating == r);
        static string P(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";
        var s = new List<string>();
        if (N(Compatibility.Rating.WorksOutOfTheBox) is > 0 and var ok) s.Add($"{P(ok, "part works", "parts work")} right away.");
        if (N(Compatibility.Rating.WorksWithExtraDriver) is > 0 and var x) s.Add($"{P(x, "part needs", "parts need")} one extra step after you install.");
        if (N(Compatibility.Rating.Partial) is > 0 and var p) s.Add($"{P(p, "part only partly works", "parts only partly work")}.");
        if (N(Compatibility.Rating.Unsupported) is > 0 and var u) s.Add($"{P(u, "part won't work", "parts won't work")}.");
        var unknown = parts.Where(x => x.Rating == Compatibility.Rating.Unknown).ToList();
        var big = unknown.Count(x => x.Importance != Importance.Minor);
        var small = unknown.Count - big;
        if (big > 0) s.Add($"We couldn't check {P(big, "important part", "important parts")}. Test {(big == 1 ? "it" : "them")} from the USB stick.");
        if (small > 0) s.Add($"{P(small, "small part", "small parts")} couldn't be checked, which rarely matters.");
        return string.Join(" ", s);
    }

    public const string SecureBoot = "Secure Boot is a safety setting. It decides what software is allowed to start your PC.";
    public const string BitLocker = "BitLocker locks your files so only you can read them.";
    public const string Distro = "There are many versions of Linux. People call them \"distros\".";

    /// <summary>"Ubuntu 26.04.1 LTS" → "Ubuntu"; "Pop!_OS 24.04" → "Pop!_OS".</summary>
    public static string ShortName(string distroName)
    {
        var i = distroName.IndexOfAny("0123456789".ToCharArray());
        return i > 1 ? distroName[..i].TrimEnd() : distroName;
    }

    /// <summary>Bytes as Windows shows them (binary GB), rounded: "about 6 GB".</summary>
    public static string AboutGb(long bytes) => $"about {Math.Max(1, (int)Math.Round(bytes / 1073741824.0))} GB";
}
