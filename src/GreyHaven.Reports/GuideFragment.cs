using System.Text.RegularExpressions;
using GreyHaven.Core.Compatibility;

namespace GreyHaven.Reports;

/// <summary>
/// The personal touches in a phone-guide address, after "#". Browsers never send this part to
/// the website. Only these fields exist, each with a strict format, and none of them identifies a
/// person or a PC: no part IDs, device or app lists, model names, serial numbers or file names.
/// The page's script (<see cref="Script"/>) is generated from the same patterns.
/// </summary>
public static partial class GuideFragment
{
    /// <summary>Field, what it holds, and the only values the page accepts.</summary>
    public static readonly IReadOnlyList<(string Key, string Meaning, string Pattern)> Fields =
    [
        ("v", "format version", "^1$"),
        ("m", "PC or motherboard maker, e.g. MSI", "^[A-Za-z0-9 .&-]{1,24}$"),
        ("k", "startup menu key, e.g. F11", "^[A-Za-z0-9 ,.()'-]{1,100}$"),
        ("s", "startup settings key, e.g. Delete", "^[A-Za-z0-9 ,-]{1,20}$"),
        ("sb", "where Secure Boot is in the startup settings", "^[A-Za-z0-9 >&/-]{1,80}$"),
        ("f", "laptop or desktop", "^(laptop|desktop)$"),
        ("g", "GB to give Linux next to Windows", "^[0-9]{2,4}$"),
        ("x", "extra steps after installing, by rule (e.g. nvidia-gpu)", "^[a-z0-9-]{1,40}(,[a-z0-9-]{1,40}){0,5}$"),
    ];

    public static bool IsValid(string key, string value) =>
        Fields.FirstOrDefault(f => f.Key == key) is { Key: not null } f && Regex.IsMatch(value, f.Pattern);

    /// <summary>The fragment for this report's choices (without "#"). Values that don't fit a field's pattern are left out.</summary>
    public static string For(CompatibilityReport r, int? giveGb)
    {
        var path = r.SelectedRecommendation ?? r.Recommendation.Paths.First();
        var values = new List<(string, string?)>
        {
            ("v", "1"),
            ("m", r.Maker?.Name),
            ("k", r.Maker?.BootMenuKey),
            ("s", path.Requirements.Contains(PreSwitchRequirement.TurnOffSecureBoot) ? r.Maker?.SetupKey : null),
            ("sb", path.Requirements.Contains(PreSwitchRequirement.TurnOffSecureBoot) ? r.Maker?.SecureBootPath : null),
            ("f", r.Profile.System.FormFactor switch { Core.Hardware.FormFactor.Laptop => "laptop", Core.Hardware.FormFactor.Desktop => "desktop", _ => null }),
            ("g", r.SelectedInstall == InstallChoice.KeepWindows ? giveGb?.ToString(System.Globalization.CultureInfo.InvariantCulture) : null),
            ("x", string.Join(",", r.Components.Select(c => c.For(path.DistroId).Fix?.Id).OfType<string>().Distinct().Order(StringComparer.Ordinal))),
        };
        return string.Join("&", values
            .Where(v => !string.IsNullOrEmpty(v.Item2) && IsValid(v.Item1, v.Item2!))
            .Select(v => $"{v.Item1}={Uri.EscapeDataString(v.Item2!)}"));
    }

    /// <summary>
    /// Fills in [data-fill] and hides [data-if] sections that don't apply: "x:id" shows only when
    /// that extra step is listed; "has:key" only when the field is there; "key:value" (e.g.
    /// "f:laptop") unless the field says something else, so it shows when the field is missing. Uses textContent only,
    /// and only for values matching <see cref="Fields"/>. With no valid "#v=1", it does nothing and
    /// the general guide shows.
    /// </summary>
    public static string Script()
    {
        var patterns = string.Join(",", Fields.Select(f => $"{f.Key}:/{f.Pattern.Replace("/", "\\/")}/"));
        return "(function(){" +
               $"var R={{{patterns}}},p={{}};" +
               "location.hash.slice(1).split('&').forEach(function(kv){var i=kv.indexOf('=');if(i<1)return;var k=kv.slice(0,i),v;" +
               "try{v=decodeURIComponent(kv.slice(i+1))}catch(e){return}if(Object.prototype.hasOwnProperty.call(R,k)&&R[k].test(v))p[k]=v});" +
               "if(p.v!=='1')return;" +
               "document.querySelectorAll('[data-fill]').forEach(function(e){var v=p[e.getAttribute('data-fill')];if(v)e.textContent=v});" +
               "var x=(p.x||'').split(',');" +
               "document.querySelectorAll('[data-if]').forEach(function(e){var c=e.getAttribute('data-if').split(':'),ok=" +
               "c[0]==='x'?x.indexOf(c[1])>=0:c[0]==='has'?!!p[c[1]]:!p[c[0]]||p[c[0]]===c[1];if(!ok)e.hidden=true})" +
               "})();";
    }
}
