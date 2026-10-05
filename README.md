# GreyHaven

GreyHaven is a Windows app that scans your PC's hardware, tells you how well it will work on
Linux (Linux Mint, Ubuntu and Pop!_OS), and helps you prepare to switch.

> **Status:** early development (milestone 1 of 8: hardware scan). Not ready for users yet.
> See [docs/PLAN.md](docs/PLAN.md) for the full plan.

## Privacy

GreyHaven reads **device IDs and system settings only**. It never reads serial numbers,
user names, your PC's network name, MAC addresses or the names of paired Bluetooth devices.
When online lookups arrive (milestone 2), only device IDs are sent.

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
```

On Windows, `greyhaven scan` scans the real PC. Use `--dump-raw <file>` to save the
unprocessed scan data; it can be replayed anywhere with `greyhaven scan --raw <file>`.

### Layout

| Path | What it is |
|---|---|
| `src/GreyHaven.Core` | Models, ID parsing, component classification, profile building (no Windows dependencies) |
| `src/GreyHaven.Scan.Windows` | Reads WMI, the registry and Win32 APIs (Windows only) |
| `src/GreyHaven.Cli` | Command-line tool for development and demos |
| `samples/` | Sample hardware snapshots for development without real hardware |
| `tests/` | Unit tests |
| `docs/windows-validation.md` | Checks that need a real Windows PC |

## License

Code: [MIT](LICENSE). Compatibility data in `data/` (from milestone 2): CC-BY-4.0.
