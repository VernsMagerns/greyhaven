# Developing GreyHaven

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

## Layout

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
| `data/` | Compatibility data you can improve without writing code ([data/README.md](../data/README.md)) |
| `tools/GreyHaven.IndexBuilder` | Builds `data/indexes/` from official distro packages |
| `tools/GreyHaven.UiSmoke` | CI UI walkthrough: screenshots and accessibility-name check (Windows) |
| `samples/` | Sample hardware snapshots for development without real hardware |
| `tests/` | Unit tests |
| `docs/scoring.md` | How ratings and the summary score are calculated |
| `docs/secure-boot.md` | General guide to turning Secure Boot off and back on |
| `docs/windows-validation.md` | Checks that need a real Windows PC |
| `docs/going-public.md` | Step-by-step checklist for making the repository public and getting code signing |
| `tools/illustrations` | Generates the walkthrough pictures (SVG for reports, XAML for the app) |
| `docs/windows-versions.md` | Windows 10 vs 11: what CI covers, differences, testing in a VM |
| `docs/windows-results.md` | Step-by-step Windows test session and results form |
| `assets/fonts` | Brand fonts Rubik and Lexend (SIL Open Font License 1.1), used by the app and HTML reports |
| `tools/ui-preview` | Throwaway browser mock-up of the app's flow for design review; never shipped |
| `tools/release-check.sh` | Fails if a placeholder (e.g. the GitHub repo name) would ship |
| `installer/` | WiX projects for `GreyHaven.msi` and `GreyHaven-Setup.exe` (built and install-tested in CI on Windows) |
| `site/`, `tools/site` | The website (https://greyhaven.pages.dev): `tools/site/build.sh` assembles it with the game list, `tools/site/test.cjs` tests it in jsdom, `.github/workflows/site.yml` deploys it to Cloudflare Pages. `site-forward/` is the old GitHub Pages address, which now forwards there. |
| `assets/brand`, `tools/brand` | The mark, icons, banners and social preview, drawn by `tools/brand/build.py` ([docs/brand.md](brand.md)) |
| `assets/sounds`, `tools/sounds` | The app's sound effects, made from scratch by `tools/sounds/build.py` |

## Releases

Push a tag like `v1.0.0`. `.github/workflows/release.yml` tests, builds the app, the MSI and
`GreyHaven-Setup.exe`, installs and uninstalls once more on Windows, writes `SHA256SUMS.txt`,
adds build attestations (public repositories) and creates a **draft** release. Nothing is public
until a maintainer reviews the notes and publishes it. The launch tracker is
[launch-checklist.md](launch-checklist.md).
