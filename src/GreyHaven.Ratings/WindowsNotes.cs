using System.Globalization;
using GreyHaven.Compat.Data;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Ratings;

/// <summary>Calm, factual context about the PC's Windows version (data/windows.yaml).</summary>
public static class WindowsNotes
{
    public static IReadOnlyList<ContextNote> For(SystemInfo system, WindowsLifecycle data, DateOnly today)
    {
        var w = system.Windows;
        // Windows 10 only (Windows 11 reports build 22000 or later). LTSC editions follow their
        // own, longer support schedule, so the note would be wrong for them.
        if (w.Build is null || w.IsWindows11 || IsLtsc(w.Edition)) return [];

        var w10 = data.Windows10;
        if (w10.EndOfSupport.Length == 0 || today < w10.EndOfSupportDate) return [];

        var esu = w10.EsuEndsDate;
        var text = $"Microsoft ended support for Windows 10 on {Day(w10.EndOfSupportDate)}. Your PC keeps working, but " +
            (today <= esu
                ? $"it only gets security updates if it's enrolled in Microsoft's Extended Security Updates program, which runs until {Day(esu)}."
                : $"it no longer gets security updates: Microsoft's Extended Security Updates program ended on {Day(esu)}.") +
            " If you were thinking about switching, this is one reason it may be worth it.";
        return [new ContextNote(text, new Source(w10.Source.Title, w10.Source.Url, SourceKind.Reference))];
    }

    private static bool IsLtsc(string? edition) =>
        edition is not null && (edition.StartsWith("EnterpriseS", StringComparison.OrdinalIgnoreCase) || edition.StartsWith("IoTEnterpriseS", StringComparison.OrdinalIgnoreCase));

    private static string Day(DateOnly d) => d.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
}
