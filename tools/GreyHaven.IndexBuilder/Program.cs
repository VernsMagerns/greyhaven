using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using GreyHaven.Compat.Data;
using GreyHaven.Compat.Kernel;
using GreyHaven.IndexBuilder;

// Builds the per-distro alias indexes in data/kernel/ from official distro packages.
//
//   greyhaven-index-builder [kernels|packages|all] [--data <dir>] [--cache <dir>] [--only <id>]
//
// Downloads are cached (default: ~/.cache/greyhaven-index) and recorded with their SHA-256 in
// each index header so the result can be audited and reproduced.

var command = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? "all";
string Opt(string name, string fallback)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
}

var dataDir = Path.GetFullPath(Opt("--data", "data"));
var cacheDir = Path.GetFullPath(Opt("--cache", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "greyhaven-index")));
var only = Opt("--only", "");
var catalog = Yaml.Load<DistroCatalog>(Path.Combine(dataDir, "distros.yaml"));
catalog.Validate();
Directory.CreateDirectory(cacheDir);

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("GreyHaven-index-builder/0.1");
var generated = DateTime.UtcNow.ToString("yyyy-MM-dd");

if (command is "kernels" or "all")
{
    foreach (var (id, kernel) in catalog.Kernels)
    {
        if (only.Length > 0 && only != id) continue;
        Console.WriteLine($"kernel {id} ({kernel.Version})");
        var entries = new List<AliasEntry>();
        var headers = new Dictionary<string, string>
        {
            ["id"] = id,
            ["kind"] = "kernel",
            ["kernel"] = kernel.Version,
            ["series"] = kernel.Series,
            ["generated"] = generated,
            ["license"] = "Derived from Linux kernel module metadata (device ID tables).",
        };
        if (kernel.VerifiedFrom is not null) headers["verified-from"] = kernel.VerifiedFrom;
        if (kernel.Approximation is not null) headers["approximation"] = Regex.Replace(kernel.Approximation, @"\s+", " ");

        for (var i = 0; i < kernel.Packages.Count; i++)
        {
            var (path, sha) = await Download(kernel.Packages[i]);
            headers[$"source-{i + 1}"] = $"{kernel.Packages[i]} sha256:{sha}";
            entries.AddRange(DebReader.ReadAliases(path, Console.WriteLine));
        }
        var outPath = Path.Combine(dataDir, kernel.Index);
        AliasIndex.Write(outPath, headers, entries);
        Console.WriteLine($"  wrote {Path.GetRelativePath(dataDir, outPath)} ({new FileInfo(outPath).Length / 1024} KiB)");
    }
}

if (command is "packages" or "all")
{
    foreach (var (id, set) in catalog.DriverPackageSets)
    {
        if (only.Length > 0 && only != id) continue;
        Console.WriteLine($"driver packages {id}");
        var entries = new List<AliasEntry>();
        var headers = new Dictionary<string, string> { ["id"] = id, ["kind"] = "packages", ["generated"] = generated };
        for (var i = 0; i < set.PackageIndexes.Count; i++)
        {
            var (path, sha) = await Download(set.PackageIndexes[i], refresh: true);
            headers[$"source-{i + 1}"] = $"{set.PackageIndexes[i]} sha256:{sha}";
            await using var gz = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
            using var reader = new StreamReader(gz);
            var found = PackageIndexReader.ReadModaliases(reader).ToList();
            Console.WriteLine($"  {Path.GetFileName(new Uri(set.PackageIndexes[i]).AbsolutePath)} {set.PackageIndexes[i].Split("/dists/")[^1]}: {found.Count} modalias patterns");
            entries.AddRange(found);
        }
        var outPath = Path.Combine(dataDir, set.Index);
        AliasIndex.Write(outPath, headers, entries);
        Console.WriteLine($"  wrote {Path.GetRelativePath(dataDir, outPath)} ({new FileInfo(outPath).Length / 1024} KiB)");
    }
}

if (command is "fingerprint" or "all")
{
    foreach (var (id, set) in catalog.FingerprintSets)
    {
        if (only.Length > 0 && only != id) continue;
        Console.WriteLine($"fingerprint {id}");
        var (path, sha) = await Download(set.Package);
        var hwdb = DebReader.ReadFiles(path, ".hwdb").SingleOrDefault(f => f.Path.Contains("libfprint", StringComparison.Ordinal));
        if (hwdb.Data is null) throw new InvalidDataException($"No libfprint hwdb in {set.Package}");
        using var reader = new StringReader(System.Text.Encoding.UTF8.GetString(hwdb.Data));
        var entries = FingerprintHwdbReader.Read(reader).ToList();
        Console.WriteLine($"  {entries.Count(e => e.Target != FingerprintHwdbReader.Unsupported)} supported, {entries.Count(e => e.Target == FingerprintHwdbReader.Unsupported)} known unsupported");
        var outPath = Path.Combine(dataDir, set.Index);
        AliasIndex.Write(outPath, new Dictionary<string, string>
        {
            ["id"] = id,
            ["kind"] = "fingerprint",
            ["libfprint"] = set.Version,
            ["generated"] = generated,
            ["source-1"] = $"{set.Package} sha256:{sha}",
            ["license"] = "Derived from libfprint's udev hwdb (LGPL-2.1-or-later).",
        }, entries);
        Console.WriteLine($"  wrote {Path.GetRelativePath(dataDir, outPath)} ({new FileInfo(outPath).Length / 1024} KiB)");
    }
}

return 0;

async Task<(string Path, string Sha256)> Download(string url, bool refresh = false)
{
    var name = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url)))[..16] + "-" + Path.GetFileName(new Uri(url).AbsolutePath);
    var path = Path.Combine(cacheDir, name);
    if (refresh || !File.Exists(path))
    {
        Console.WriteLine($"  downloading {url}");
        var tmp = path + ".part";
        using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
        {
            resp.EnsureSuccessStatusCode();
            await using var fs = File.Create(tmp);
            await resp.Content.CopyToAsync(fs);
        }
        File.Move(tmp, path, overwrite: true);
    }
    await using var f = File.OpenRead(path);
    return (path, Convert.ToHexStringLower(await SHA256.HashDataAsync(f)));
}
