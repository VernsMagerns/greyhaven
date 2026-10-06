using GreyHaven.Cli;
using GreyHaven.Compat;
using GreyHaven.Compat.Online;
using GreyHaven.Core;
using GreyHaven.Core.Compatibility;
using GreyHaven.Core.Scanning;
using GreyHaven.Core.Serialization;
using GreyHaven.Ratings;
using GreyHaven.Reports;
using GreyHaven.Scan.Windows;

return await CliApp.RunAsync(args);

namespace GreyHaven.Cli
{
    internal static class CliApp
    {
        private const string Usage = """
            GreyHaven - check how well this PC will work on Linux.

            Usage:
              greyhaven scan                   Scan this PC (Windows only, no admin needed)
              greyhaven scan --sample <name>   Use a bundled sample profile instead of real hardware
              greyhaven scan --raw <file>      Replay a raw snapshot saved with --dump-raw
              greyhaven rate [scan options]    Scan, then rate every part for Mint, Ubuntu and Pop!_OS
              greyhaven samples                List bundled sample profiles
              greyhaven check-releases         Check for distro releases newer than the bundled data

            Options for scan:
              --json              Print the profile as JSON instead of a readable summary
              --out <file>        Also write the profile JSON to a file
              --dump-raw <file>   (live scan) Save the unprocessed scan data for bug reports
              --samples-dir <dir> Look for samples in this directory

            Options for rate:
              --offline           Don't query linux-hardware.org (built-in data only)
              --distro <id>       Show details for one distro only (mint, ubuntu, pop-os)
              --path <kind>       Choose the recommendation path: easy or advanced
                                  (offered when the best distro needs Secure Boot turned off)
              --json              Print the full report as JSON
              --out <file>        Save the report; the format follows the name: .md (Markdown),
                                  .html (a single web page) or .json. Can be given more than once.
              --data <dir>        Use compatibility data from this directory

            Privacy: scans read device IDs and settings only - never serial numbers,
            user names, host names or MAC addresses. Online lookups send device IDs only.
            """;

        public static async Task<int> RunAsync(string[] args)
        {
            try
            {
                if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
                {
                    Console.WriteLine(Usage);
                    return args.Length == 0 ? 1 : 0;
                }
                if (args[0] is "--version")
                {
                    Console.WriteLine($"{GreyHavenInfo.ProductName} {GreyHavenInfo.Version}");
                    return 0;
                }

                var opts = Options.Parse(args.Skip(1).ToArray());
                return args[0] switch
                {
                    "scan" => await ScanAsync(opts),
                    "rate" => await RateAsync(opts),
                    "samples" => ListSamples(opts),
                    "check-releases" => await CheckReleasesAsync(opts),
                    _ => Fail($"Unknown command '{args[0]}'.\n\n{Usage}"),
                };
            }
            catch (CliException ex)
            {
                return Fail(ex.Message);
            }
        }

        private static async Task<int> ScanAsync(Options opts)
        {
            var profile = await (await CreateScannerAsync(opts)).ScanAsync();
            var json = GreyHavenJson.Serialize(profile);
            if (opts.Out.Count > 1) throw new CliException("scan writes one profile file; give --out once.");
            if (opts.Out.Count == 1) await File.WriteAllTextAsync(opts.Out[0], json);
            Console.WriteLine(opts.Json ? json : ProfileTextFormatter.Format(profile));
            return 0;
        }

        private static async Task<int> RateAsync(Options opts)
        {
            var profile = await (await CreateScannerAsync(opts)).ScanAsync();
            var data = CompatibilityData.Load(opts.DataDir);

            using var http = GreyHavenHttp.Create(TimeSpan.FromSeconds(30));
            var community = new LinuxHardwareDbClient(http, LinuxHardwareDbClient.DefaultCacheDirectory);
            var engine = new RatingEngine(data, community, new DistroReleaseChecker(http));

            IProgress<RatingProgress>? progress = opts.Json ? null : new Progress<RatingProgress>(p =>
                Console.Error.WriteLine(p.Total is null ? $"{p.Message}..." : $"{p.Message} ({p.Current} of {p.Total}, one request every 10 seconds; use --offline to skip)..."));
            var report = await engine.RateAsync(profile, new RatingOptions { UseOnlineLookups = !opts.Offline }, progress);
            if (opts.Path is { } path)
            {
                var kind = path switch
                {
                    "easy" => PathKind.Easy,
                    "advanced" => PathKind.Advanced,
                    _ => throw new CliException("--path must be 'easy' or 'advanced'."),
                };
                if (report.Recommendation.Get(kind) is null)
                    throw new CliException($"This PC has no '{path}' path: it is only offered when the best distro needs Secure Boot turned off.");
                report = report.WithPath(kind);
            }

            foreach (var file in opts.Out)
            {
                var format = ReportExporter.FormatFromPath(file) ?? throw new CliException($"Can't tell the format of '{file}'. Use a name ending in .md, .html or .json.");
                try { await ReportExporter.SaveAsync(report, file, format, opts.Distro); }
                catch (ArgumentException ex) { throw new CliException(ex.Message); }
                if (!opts.Json) Console.Error.WriteLine($"Saved {file}");
            }
            Console.WriteLine(opts.Json ? ReportExporter.Render(report, ReportFormat.Json) : ReportTextFormatter.Format(report, opts.Distro));
            return 0;
        }

        /// <summary>Used by the scheduled GitHub Action to open an issue when a distro releases a new version.</summary>
        private static async Task<int> CheckReleasesAsync(Options opts)
        {
            var data = CompatibilityData.Load(opts.DataDir);
            using var http = GreyHavenHttp.Create(TimeSpan.FromSeconds(30));
            var result = await new DistroReleaseChecker(http).CheckAsync(data.Distros);
            if (opts.Json)
            {
                Console.WriteLine(GreyHavenJson.Serialize(new { result.Newer, result.Errors }));
            }
            else
            {
                if (result.Newer.Count == 0) Console.WriteLine("No newer releases found.");
                foreach (var n in result.Newer) Console.WriteLine($"{n.DistroName}: data covers {n.Current}, found {n.Found}");
                foreach (var e in result.Errors) Console.Error.WriteLine(e);
            }
            return result.Complete ? 0 : 2;
        }

        private static async Task<IHardwareScanner> CreateScannerAsync(Options opts)
        {
            IHardwareScanner scanner;
            if (opts.Sample is not null)
            {
                scanner = SampleProfiles.Open(opts.Sample, opts.SamplesDir);
            }
            else if (opts.Raw is not null)
            {
                if (!File.Exists(opts.Raw)) throw new CliException($"File not found: {opts.Raw}");
                scanner = new SnapshotFileScanner(opts.Raw);
            }
            else if (OperatingSystem.IsWindows())
            {
                var source = new WindowsRawHardwareSource();
                if (opts.DumpRaw is not null)
                {
                    var raw = await source.CaptureAsync();
                    await File.WriteAllTextAsync(opts.DumpRaw, GreyHavenJson.Serialize(raw));
                    Console.Error.WriteLine($"Raw scan data written to {opts.DumpRaw}");
                    scanner = new SnapshotFileScanner(opts.DumpRaw, "live-scan");
                }
                else
                {
                    scanner = new LiveScanner(source);
                }
            }
            else
            {
                throw new CliException(
                    "Scanning real hardware needs Windows. On this system, use a sample profile, e.g.:\n" +
                    "  greyhaven scan --sample intel-laptop\n" +
                    "Run 'greyhaven samples' to list them.");
            }
            return scanner;
        }

        private static int ListSamples(Options opts)
        {
            var samples = SampleProfiles.List(opts.SamplesDir);
            if (samples.Count == 0) return Fail("No sample profiles found.");
            foreach (var s in samples) Console.WriteLine(s);
            return 0;
        }

        private static int Fail(string message)
        {
            Console.Error.WriteLine(message);
            return 1;
        }
    }

    internal sealed class CliException(string message) : Exception(message);

    internal sealed record Options(string? Sample, string? Raw, bool Json, IReadOnlyList<string> Out, string? DumpRaw, string? SamplesDir,
        bool Offline = false, string? Distro = null, string? DataDir = null, string? Path = null)
    {
        public static Options Parse(string[] args)
        {
            var @out = new List<string>();
            string? sample = null, raw = null, dumpRaw = null, samplesDir = null, distro = null, dataDir = null, path = null;
            var json = false;
            var offline = false;
            for (var i = 0; i < args.Length; i++)
            {
                string Value() => i + 1 < args.Length ? args[++i] : throw new CliException($"{args[i]} needs a value.");
                switch (args[i])
                {
                    case "--sample": sample = Value(); break;
                    case "--raw": raw = Value(); break;
                    case "--json": json = true; break;
                    case "--out": @out.Add(Value()); break;
                    case "--dump-raw": dumpRaw = Value(); break;
                    case "--samples-dir": samplesDir = Value(); break;
                    case "--offline": offline = true; break;
                    case "--distro": distro = Value(); break;
                    case "--data": dataDir = Value(); break;
                    case "--path": path = Value(); break;
                    default: throw new CliException($"Unknown option '{args[i]}'.");
                }
            }
            if (sample is not null && raw is not null) throw new CliException("Use either --sample or --raw, not both.");
            if (dumpRaw is not null && (sample is not null || raw is not null)) throw new CliException("--dump-raw only applies to a live scan.");
            return new Options(sample, raw, json, @out, dumpRaw, samplesDir, offline, distro, dataDir, path);
        }
    }
}
