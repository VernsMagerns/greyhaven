using GreyHaven.Compat;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Hardware;

namespace GreyHaven.Ratings;

/// <summary>
/// The pages of the public phone guide, from the data files alone: one per distro, download and
/// install choice. No profile or report goes in, so nothing about any PC can end up on them.
/// </summary>
public static class PhoneGuideSite
{
    public static IReadOnlyList<PhoneGuidePage> Pages(CompatibilityData data)
    {
        var pages = new List<PhoneGuidePage>();
        foreach (var d in data.Distros.Distros)
        {
            // Checks for any PC; the touchpad, battery and lid only on laptops; the sleep test
            // worded for each kind of PC.
            static bool LaptopOnly(Compat.Data.LiveUsbCheckInfo c) => c.Categories.All(x => x == "touchpad");
            var checks = data.LiveUsb.Checks.Where(c => !LaptopOnly(c)).Select(c => new LiveUsbCheck(c.Title, c.Test)).ToList();
            var laptop = data.LiveUsb.Checks.Where(LaptopOnly).Select(c => new LiveUsbCheck(c.Title, c.Test))
                .Concat(data.Caveats.Caveats.Where(c => c.AppliesWhen.Always || c.AppliesWhen.FormFactor == "laptop").Select(c => new LiveUsbCheck(c.Title, c.TestFor("laptop"))))
                .ToList();
            var desktop = data.Caveats.Caveats.Where(c => c.AppliesWhen.Always || c.AppliesWhen.FormFactor == "desktop").Select(c => new LiveUsbCheck(c.Title, c.TestFor("desktop"))).ToList();
            foreach (var e in d.Editions)
            {
                var fixes = data.Rules.Rules
                    .Where(r => r.Fix is not null && !EditionHasDriver(r, e))
                    .Select(r => new PhoneGuideFix(r.Id, r.Title ?? r.Fix!.Driver, RatingEngine.BuildFix(r.Fix!, d, SecureBootState.Unknown) with { Id = r.Id }))
                    .Where(f => f.Fix.Steps.Count > 0 || f.Fix.Commands.Count > 0)
                    .ToList();
                var name = PlainWords.ShortName(d.DisplayName);
                foreach (var install in new[] { InstallChoice.KeepWindows, InstallChoice.ReplaceWindows })
                    if (install == InstallChoice.ReplaceWindows || PhoneGuidePages.CanKeepWindows(d.Id))
                        pages.Add(new PhoneGuidePage(d.Id, name, e.Id, e.Name, install, checks, laptop, desktop, fixes));
            }
        }
        return pages;
    }

    /// <summary>True when the rule says this download already includes the driver (e.g. Pop!_OS's NVIDIA ISO).</summary>
    private static bool EditionHasDriver(Compat.Rules.Rule rule, Compat.Data.DistroEdition edition) =>
        rule.Ratings.Any(o => o.When?.PreinstalledDriver is { } driver && edition.PreinstalledDrivers.Contains(driver));
}
