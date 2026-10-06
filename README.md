# GreyHaven

GreyHaven is a Windows app that scans your PC's hardware, tells you how well it will work on
Linux (Linux Mint, Ubuntu and Pop!_OS), and helps you prepare to switch.

> **Status:** early development (milestone 4 of 8: reports and export). Not ready for users yet.
> See [docs/PLAN.md](docs/PLAN.md) for the full plan.

## Privacy

GreyHaven reads **device IDs and system settings only**. It never reads serial numbers,
user names, your PC's network name, MAC addresses or the names of paired Bluetooth devices.

Ratings work **offline** from data shipped with the app. Only for devices GreyHaven doesn't
recognise, it can look up the device ID on [linux-hardware.org](https://linux-hardware.org):
the request contains only the device ID (e.g. `pci:8086-2723`), at most one request every
10 seconds. You can turn this off (`--offline`).

## Accessibility

The app supports full keyboard navigation, screen readers (named controls, headings, announced
progress), light, dark and High Contrast themes, and WCAG AA colour contrast. Ratings are
always shown as an icon plus text, never colour alone. Tests enforce the colour contrast
and check the views for unnamed controls.

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
dotnet run --project src/GreyHaven.Cli -- rate --sample nvidia-desktop --offline --out report.html --out report.md
```

The Windows app is in `src/GreyHaven.App` (WPF). It builds on Linux but runs only on Windows.
Every push builds self-contained copies of the app and CLI, downloadable from the GitHub
Actions run as **greyhaven-windows**; `./tools/publish-windows.sh` builds the same locally. `GreyHaven.exe --sample nvidia-desktop` runs the app
in demo mode on a sample PC.

On Windows, `greyhaven scan` scans the real PC. Use `--dump-raw <file>` to save the
unprocessed scan data; it can be replayed anywhere with `greyhaven scan --raw <file>`.

### Layout

| Path | What it is |
|---|---|
| `src/GreyHaven.Core` | Models, ID parsing, component classification, profile building (no Windows dependencies) |
| `src/GreyHaven.Scan.Windows` | Reads WMI, the registry and Win32 APIs (Windows only) |
| `src/GreyHaven.Compat` | Compatibility data: distro catalog, driver indexes, curated rules, linux-hardware.org client |
| `src/GreyHaven.Ratings` | Rating engine, summary score, distro recommendation |
| `src/GreyHaven.Reports` | Report export: Markdown, self-contained HTML, JSON, live-USB guide |
| `src/GreyHaven.Presentation` | View models, colour palette and settings for the UI (no WPF; tested on Linux) |
| `src/GreyHaven.App` | The Windows app (WPF views, theming) |
| `src/GreyHaven.Cli` | Command-line tool for development and demos |
| `data/` | Compatibility data you can improve without writing code ([data/README.md](data/README.md)) |
| `tools/GreyHaven.IndexBuilder` | Builds `data/indexes/` from official distro packages |
| `tools/GreyHaven.UiSmoke` | CI UI walkthrough: screenshots and accessibility-name check (Windows) |
| `samples/` | Sample hardware snapshots for development without real hardware |
| `tests/` | Unit tests |
| `docs/scoring.md` | How ratings and the summary score are calculated |
| `docs/secure-boot.md` | General guide to turning Secure Boot off and back on |
| `docs/windows-validation.md` | Checks that need a real Windows PC |
| `docs/windows-results.md` | Step-by-step Windows test session and results form |
| `tools/ui-preview` | Throwaway browser mock-up of the app's flow for design review; never shipped |
| `tools/release-check.sh` | Fails if a placeholder (e.g. the GitHub repo name) would ship |

## License

Code: [MIT](LICENSE). Curated compatibility data in `data/`: CC-BY-4.0 (see [data/LICENSE](data/LICENSE)).
