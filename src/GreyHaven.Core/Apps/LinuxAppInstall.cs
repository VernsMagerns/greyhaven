namespace GreyHaven.Core.Apps;

/// <summary>Where to get a Linux app on one distro: the app store to open, or a website, and a command for the terminal.</summary>
/// <param name="Where">Plain words, e.g. "Software Manager" or "Download it from the website".</param>
/// <param name="Link">A page to open (the app's Flathub or Snap Store page, or its website).</param>
/// <param name="Command">One line for a terminal, if there's a safe one-step install.</param>
public sealed record LinuxAppInstall(string Where, string? Link, string? Command);

/// <summary>
/// The app stores, as each distro ships them (checked 2026-10-07):
/// <list type="bullet">
///   <item>Ubuntu: App Center shows Snap packages; Flathub needs a one-time setup (flathub.org/setup/Ubuntu).</item>
///   <item>Linux Mint: Software Manager shows Flathub apps (Flathub is on by default) but hides
///     unverified ones by default, plus Ubuntu packages (apt).</item>
///   <item>Pop!_OS 24.04: COSMIC Store with Flathub, Ubuntu and Pop!_OS repositories on by default.</item>
/// </list>
/// </summary>
public static class LinuxAppInstalls
{
    public static string StoreName(string distroId) => distroId switch
    {
        "mint" => "Software Manager",
        "pop-os" => "COSMIC Store",
        _ => "App Center",
    };

    public static LinuxAppInstall For(LinuxApp app, string distroId)
    {
        if (app.Web is { } web) return new("Open it in your web browser", web, null);
        if (app.Preinstalled.Contains(distroId)) return new("Already installed", null, null);
        var store = StoreName(distroId);
        var flathub = app.Flathub is { } id ? $"https://flathub.org/apps/{id}" : null;
        var flatpak = app.Flathub is { } fid ? $"flatpak install -y flathub {fid}" : null;
        var apt = app.Apt is { } pkg ? $"sudo apt install -y {pkg}" : null;

        switch (distroId)
        {
            case "ubuntu":
                if (app.Snap is { } snap) return new(store, $"https://snapcraft.io/{snap}", $"sudo snap install {snap}{(app.Classic ? " --classic" : "")}");
                if (apt is not null) return new(store, null, apt);
                if (app.Website is { } site) return new("Download it from the website", site, null);
                return new("Flathub (set up Flatpak once first: flathub.org/setup/Ubuntu)", flathub, flatpak);
            case "mint":
                if (app.Flathub is not null && app.FlathubVerified) return new(store, flathub, flatpak);
                if (apt is not null) return new(store, null, apt);
                if (app.Website is { } mintSite) return new("Download it from the website", mintSite, flatpak);
                return new($"{store} (turn on \"unverified Flatpaks\" in its settings) or a terminal", flathub, flatpak);
            default:
                if (app.Flathub is not null) return new(store, flathub, flatpak);
                if (apt is not null) return new(store, null, apt);
                return new("Download it from the website", app.Website, null);
        }
    }
}
