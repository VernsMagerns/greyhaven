using GreyHaven.Cli;
using GreyHaven.Core;
using GreyHaven.Core.Scanning;
using GreyHaven.Core.Serialization;
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
              greyhaven samples                List bundled sample profiles

            Options for scan:
              --json              Print the profile as JSON instead of a readable summary
              --out <file>        Also write the profile JSON to a file
              --dump-raw <file>   (live scan) Save the unprocessed scan data for bug reports
              --samples-dir <dir> Look for samples in this directory

            Privacy: scans read device IDs and settings only - never serial numbers,
            user names, host names or MAC addresses.
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
                    "samples" => ListSamples(opts),
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

            var profile = await scanner.ScanAsync();
            var json = GreyHavenJson.Serialize(profile);
            if (opts.Out is not null) await File.WriteAllTextAsync(opts.Out, json);
            Console.WriteLine(opts.Json ? json : ProfileTextFormatter.Format(profile));
            return 0;
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

    internal sealed record Options(string? Sample, string? Raw, bool Json, string? Out, string? DumpRaw, string? SamplesDir)
    {
        public static Options Parse(string[] args)
        {
            string? sample = null, raw = null, @out = null, dumpRaw = null, samplesDir = null;
            var json = false;
            for (var i = 0; i < args.Length; i++)
            {
                string Value() => i + 1 < args.Length ? args[++i] : throw new CliException($"{args[i]} needs a value.");
                switch (args[i])
                {
                    case "--sample": sample = Value(); break;
                    case "--raw": raw = Value(); break;
                    case "--json": json = true; break;
                    case "--out": @out = Value(); break;
                    case "--dump-raw": dumpRaw = Value(); break;
                    case "--samples-dir": samplesDir = Value(); break;
                    default: throw new CliException($"Unknown option '{args[i]}'.");
                }
            }
            if (sample is not null && raw is not null) throw new CliException("Use either --sample or --raw, not both.");
            if (dumpRaw is not null && (sample is not null || raw is not null)) throw new CliException("--dump-raw only applies to a live scan.");
            return new Options(sample, raw, json, @out, dumpRaw, samplesDir);
        }
    }
}
