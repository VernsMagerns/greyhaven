# GreyHaven

GreyHaven is a Windows app that scans your PC's hardware, tells you how well it will work on
Linux (Linux Mint, Ubuntu and Pop!_OS), and helps you prepare to switch.

> **Status:** early development (milestone 2 of 8: lookup and ratings). Not ready for users yet.
> See [docs/PLAN.md](docs/PLAN.md) for the full plan.

## Privacy

GreyHaven reads **device IDs and system settings only**. It never reads serial numbers,
user names, your PC's network name, MAC addresses or the names of paired Bluetooth devices.

Ratings work **offline** from data shipped with the app. Only for devices GreyHaven doesn't
recognise, it can look up the device ID on [linux-hardware.org](https://linux-hardware.org):
the request contains only the device ID (e.g. `pci:8086-2723`), at most one request every
10 seconds. You can turn this off (`--offline`).

## Admin rights

Scanning does **not** need administrator rights. Later features that change settings or
write to a USB drive will ask for permission (UAC) only for that one action.

## Developing

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). Everything except the live
scan builds, tests and runs on Linux and macOS.

```sh
dotnet test                                   # unit tests
dotnet run --project src/GreyHaven.Cli -- samples
dotnet run --project src/GreyHaven.Cli -- scan --sample intel-vmd-laptop
dotnet run --project src/GreyHaven.Cli -- scan --sample nvidia-desktop --json
dotnet run --project src/GreyHaven.Cli -- rate --sample nvidia-desktop --offline
dotnet run --project src/GreyHaven.Cli -- rate --sample intel-vmd-laptop --offline --distro ubuntu
```

On Windows, `greyhaven scan` scans the real PC. Use `--dump-raw <file>` to save the
unprocessed scan data; it can be replayed anywhere with `greyhaven scan --raw <file>`.

### Layout

| Path | What it is |
|---|---|
| `src/GreyHaven.Core` | Models, ID parsing, component classification, profile building (no Windows dependencies) |
| `src/GreyHaven.Scan.Windows` | Reads WMI, the registry and Win32 APIs (Windows only) |
| `src/GreyHaven.Compat` | Compatibility data: distro catalog, driver indexes, curated rules, linux-hardware.org client |
| `src/GreyHaven.Ratings` | Rating engine, summary score, distro recommendation |
| `src/GreyHaven.Cli` | Command-line tool for development and demos |
| `data/` | Compatibility data you can improve without writing code ([data/README.md](data/README.md)) |
| `tools/GreyHaven.IndexBuilder` | Builds `data/indexes/` from official distro packages |
| `samples/` | Sample hardware snapshots for development without real hardware |
| `tests/` | Unit tests |
| `docs/scoring.md` | How ratings and the summary score are calculated |
| `docs/windows-validation.md` | Checks that need a real Windows PC |

## License

Code: [MIT](LICENSE). Curated compatibility data in `data/`: CC-BY-4.0 (see [data/LICENSE](data/LICENSE)).
