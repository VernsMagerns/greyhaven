using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;
using GreyHaven.Core.Apps;
using Microsoft.Win32;

namespace GreyHaven.Scan.Windows;

/// <summary>
/// Lists installed apps without administrator rights: Windows' uninstall lists (for the PC and for
/// this user), Microsoft Store apps registered for this user, Steam libraries and Epic Games
/// manifests. Only names, publishers and store IDs are kept: never install folders, versions or
/// file paths. Filtering (updates, runtimes, drivers) happens in Core (AppInventory), so it's tested
/// on any OS. The Windows user name is replaced with "&lt;user&gt;" in names.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class WindowsApps
{
    private const string Uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string StorePackages = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

    public static IReadOnlyList<RawApp> Read()
    {
        var apps = new List<RawApp>();
        foreach (var (hive, view) in new[] { (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry32), (RegistryHive.CurrentUser, RegistryView.Default) })
            apps.AddRange(Safe(() => ReadUninstall(hive, view)));
        apps.AddRange(Safe(ReadStore));
        apps.AddRange(Safe(ReadSteam));
        apps.AddRange(Safe(ReadEpic));
        return apps.Select(Scrub).ToList();
    }

    private static IEnumerable<RawApp> Safe(Func<IEnumerable<RawApp>> read)
    {
        try { return read().ToList(); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or JsonException) { return []; }
    }

    /// <summary>Some apps put the Windows user name in their name ("Roblox Player for Sam").</summary>
    private static RawApp Scrub(RawApp a)
    {
        var user = Environment.UserName;
        if (user.Length < 2) return a;
        return a with
        {
            Name = a.Name.Replace(user, "<user>", StringComparison.OrdinalIgnoreCase),
            Publisher = a.Publisher?.Replace(user, "<user>", StringComparison.OrdinalIgnoreCase),
        };
    }

    private static IEnumerable<RawApp> ReadUninstall(RegistryHive hive, RegistryView view)
    {
        using var root = RegistryKey.OpenBaseKey(hive, view);
        using var list = root.OpenSubKey(Uninstall);
        if (list is null) yield break;
        foreach (var name in list.GetSubKeyNames())
        {
            using var k = list.OpenSubKey(name);
            if (k?.GetValue("DisplayName") is not string display || string.IsNullOrWhiteSpace(display)) continue;
            // Only the Steam app ID is taken from the uninstall command, never the command itself.
            var steam = k.GetValue("UninstallString") is string u ? SteamUninstall().Match(u) : Match.Empty;
            yield return new RawApp
            {
                Name = display,
                Publisher = k.GetValue("Publisher") as string,
                Source = steam.Success ? AppSource.Steam : AppSource.Installed,
                Id = steam.Success ? steam.Groups[1].Value : null,
                SystemComponent = k.GetValue("SystemComponent") is int sc && sc == 1,
                ReleaseType = k.GetValue("ReleaseType") as string,
                HasParent = k.GetValue("ParentKeyName") is string p && p.Length > 0,
            };
        }
    }

    private static IEnumerable<RawApp> ReadStore()
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
        using var packages = root.OpenSubKey(StorePackages);
        if (packages is null) yield break;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var full in packages.GetSubKeyNames())
        {
            // Full name: Name_Version_Arch_ResourceId_PublisherId. Resource packages ("~" or "split") aren't apps.
            var parts = full.Split('_');
            if (parts.Length < 5 || parts[3].Length > 0 || !seen.Add(parts[0])) continue;
            using var k = packages.OpenSubKey(full);
            var display = k?.GetValue("DisplayName") as string;
            if (display is not null && display.StartsWith('@')) display = LoadIndirect(display);
            yield return new RawApp { Name = display ?? parts[0], Source = AppSource.Store, Id = parts[0] };
        }
    }

    private static IEnumerable<RawApp> ReadSteam()
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
        using var steam = root.OpenSubKey(@"Software\Valve\Steam");
        if (steam?.GetValue("SteamPath") is not string steamPath) yield break;
        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        var libraries = File.Exists(vdf) ? SteamFiles.LibraryFolders(File.ReadAllText(vdf)).ToList() : [];
        libraries.Add(steamPath);
        foreach (var lib in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var apps = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(apps)) continue;
            foreach (var acf in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
                if (SteamFiles.Manifest(File.ReadAllText(acf)) is { } m && !SteamFiles.IsTool(m.AppId, m.Name))
                    yield return new RawApp { Name = m.Name, Publisher = null, Source = AppSource.Steam, Id = m.AppId };
        }
    }

    private static IEnumerable<RawApp> ReadEpic()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(dir)) yield break;
        foreach (var item in Directory.EnumerateFiles(dir, "*.item"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(item));
            var r = doc.RootElement;
            if (r.TryGetProperty("bIsIncompleteInstall", out var inc) && inc.ValueKind == JsonValueKind.True) continue;
            if (!r.TryGetProperty("DisplayName", out var name) || name.GetString() is not { Length: > 0 } display) continue;
            var ns = r.TryGetProperty("CatalogNamespace", out var n) ? n.GetString() : null;
            yield return new RawApp { Name = display, Source = AppSource.Epic, Id = ns };
        }
    }

    /// <summary>Resolves a Store app's "@{Package?ms-resource://...}" display name.</summary>
    private static unsafe string? LoadIndirect(string source)
    {
        var buffer = stackalloc char[512];
        return SHLoadIndirectString(source, buffer, 512, 0) == 0 ? new string(buffer) : null;
    }

    [LibraryImport("shlwapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static unsafe partial int SHLoadIndirectString(string pszSource, char* pszOutBuf, uint cchOutBuf, nint ppvReserved);

    [GeneratedRegex(@"^\s*""?steam://uninstall/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SteamUninstall();
}
