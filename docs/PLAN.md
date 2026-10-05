# GreyHaven — Project Plan

GreyHaven is an open-source Windows desktop app that scans a PC's hardware, rates
how well each component will work on Linux (per distro), recommends a distro, and
helps the user prepare to switch: a pre-switch checklist, safe one-click fixes,
a verified live-USB creator, and shareable reports.

This document records the agreed plan. Decisions marked **(decided)** were
confirmed by the maintainer on 2026-10-04.

---

## 1. Data-source findings (investigated 2026-10-04)

### linux-hardware.org (LHDB)

| Question | Finding |
|---|---|
| API | None documented. Device data is HTML only, e.g. `https://linux-hardware.org/?id=pci:8086-2723`. Parts of the page are rendered client-side. |
| Rate limits | `robots.txt` sets `Crawl-delay: 10`. The first device-page request during research returned `429 Too Many Requests`. |
| Reliability | The site's TLS certificate expired on 2026-10-01 and was still expired at the time of research. Strict HTTPS clients cannot connect. |
| Freshness | The kernel-driver (LKDDb) table on device pages stops at kernel 6.3. |
| Bulk data | `github.com/linuxhw/Drivers` (CC-BY-4.0) and `github.com/linuxhw/DevicePopulation` (last updated 2023) are reference lists, not per-device "works/fails" data. |
| Submissions from Windows | Not supported. `hw-probe` runs on Linux/BSD only; no documented upload endpoint. |

### Data-source order (decided)

1. **Curated rules dataset** (`data/rules/*.yaml`) keyed by `vendor:device[:subvendor:subdevice]`.
   Each entry carries rating, plain-English explanation, per-distro fixes and a source link.
   The only route to **high** confidence.
2. **Per-distro kernel alias index**, built in CI from each distro's current kernel
   package (`modules.alias`). Answers "does this distro's kernel ship a driver that
   claims this ID?". Ratings resting only on this are **medium** confidence.
3. **Supplements**: `pci.ids` / `usb.ids` (names only), libfprint supported-device list,
   linux-firmware manifest.
4. **LHDB** as an optional, rate-limited (≥10 s between requests), long-cached supplement
   that degrades gracefully when unreachable. Always linked as a source when used.

No match anywhere → **Unknown**. Names are never used to guess a rating.

### Contribution channel (decided)

- **v1**: a pre-filled GitHub issue URL containing only device IDs and class. The user
  reviews exactly what will be posted in the browser and submits it themselves. A GitHub
  Action triages accepted reports into dataset PRs. Requires a GitHub account; the issue
  is public.
- The submission layer sits behind an `IContributionChannel` interface so an anonymous
  hosted endpoint can be added later without reworking the app.
- The live-USB guide asks users to run `hw-probe` from the live session, which does
  upload real Linux test results to LHDB.

---

## 2. Tech stack

**C# / .NET 10 (LTS) + WPF with the built-in Fluent theme.**

- WMI (`System.Management`) and SetupAPI are first-class from .NET (CsWin32 for P/Invoke).
- Raw disk writes for USB flashing (`DeviceIoControl`, `\\.\PhysicalDriveN`) are native
  interop in any stack; Electron/Tauri would add a second language for no gain.
- WPF Fluent theme follows the system light/dark setting; WPF has mature UI Automation
  (screen reader) support. WinUI 3 has more packaging friction and fewer contributors.
- Installer: WiX Toolset MSI (Start Menu shortcut, uninstaller), built by GitHub Actions.
  Self-contained x64 publish so users don't need .NET installed (~70–90 MB).
- Libraries: CommunityToolkit.Mvvm, YamlDotNet, SQLite (lookup cache), BouncyCastle (GPG),
  xUnit.

Everything except `Scan.Windows`, `Elevated`, `UsbCreator`'s disk I/O and `App` targets
plain `net10.0`, so the core builds, tests and demos on Linux.

---

## 3. Architecture

```
src/
  GreyHaven.Core/          models + interfaces, no Windows deps
  GreyHaven.Scan.Windows/  WMI/SetupAPI scanner -> HardwareProfile
  GreyHaven.Compat/        lookup sources, cache, offline handling
  GreyHaven.Rating/        rating engine, summary score, recommendation, checklist
  GreyHaven.Fixes/         data-driven fix catalog + one-click actions (Preview/Apply/Undo)
  GreyHaven.Reports/       Markdown, self-contained HTML, JSON
  GreyHaven.UsbCreator/    download, SHA256 + GPG verification, drive listing
  GreyHaven.Elevated/      UAC helper exe, started only for one action at a time
  GreyHaven.App/           WPF UI
  GreyHaven.Cli/           dev/demo tool; runs on Linux against sample profiles
data/                      rules/*.yaml, distros.yaml, fixes/*.yaml, kernel indexes (CC-BY-4.0)
samples/                   sample hardware profiles
tools/build-index/         CI job that builds kernel alias indexes
tests/
```

Contributors improve ratings by editing `data/` — no C# required.

### Scanning without admin

| Item | Source (non-admin) |
|---|---|
| Device IDs | PnP hardware IDs (WMI `Win32_PnPEntity` / SetupAPI) |
| Secure Boot | `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State\UEFISecureBootEnabled` |
| UEFI vs legacy | `GetFirmwareType` |
| BitLocker | Shell property `System.Volume.BitLockerProtection` (the WMI class needs admin) |
| Storage mode | PCI class code (0106 AHCI / 0104 RAID / 0108 NVMe) + known VMD/RST controller IDs |
| Fast Startup | `HKLM\...\Session Manager\Power\HiberbootEnabled` |
| Architecture | `IsWow64Process2` native machine (x64 app runs emulated on ARM64, so it can warn) |

### Ratings

Per component, per distro: one of *Works out of the box*, *Works with extra driver/config*,
*Partial/known issues*, *Unsupported*, *Unknown* — each with explanation, source link and
confidence (high/medium/low).

- NVIDIA → "extra driver"; advice depends on Secure Boot (MOK enrollment on Ubuntu/Mint;
  Pop!_OS NVIDIA ISO note).
- Sleep/suspend, hybrid graphics, battery life, fingerprint readers → always flagged
  "can't be judged from Windows" and linked to the live-USB guide.

### Summary score

- Points: out of the box 100, extra driver 80, partial 40, unsupported 0.
  Unknown is excluded from the average but lowers a displayed **coverage** figure.
- Weights: critical ×3 (storage, GPU, Ethernet, and Wi-Fi on laptops or machines without
  wired Ethernet); important ×1.5 (audio, Bluetooth, touchpad, webcam); other ×1.
- Caps: any unsupported critical → max 40. Any unknown critical → max 70, labelled "incomplete".
- Bands: Great ≥90, Good ≥70, Fair ≥50, Poor <50.
- Computed per distro; the best is recommended (ties → fewer "extra driver" steps).

### Distros (decided)

Current releases only: Ubuntu LTS, Linux Mint (Cinnamon), Pop!_OS. `distros.yaml` is
structured so more editions can be added as data, not code.

### Safety

- One-click fixes only for safe, reversible changes (e.g. Fast Startup via
  `HiberbootEnabled`, not `powercfg /h off`). Each shows the exact change, requires
  confirmation, elevates for that action only, creates a restore point where possible
  (Windows allows one per 24 h by default), and records the old value for Undo.
- AHCI switching, BIOS/firmware updates, BitLocker changes: instructions only.
- USB flashing: the elevated helper re-validates the target itself (USB bus, not the
  system/boot disk, no page file, identity matches the user's selection), writes, reads
  back to verify, and supports cancel. ISOs come only from official URLs pinned in
  `distros.yaml`; SHA256 is verified against the official checksum file and GPG against
  pinned key fingerprints. A mismatch is never flashed.

---

## 4. Project settings (decided)

- **Name**: GreyHaven.
- **License**: MIT for code, CC-BY-4.0 for `data/`.
- **GitHub owner/repo**: not yet chosen. Stored in a single config value
  (`GreyHavenInfo.GitHubRepository`) with a placeholder. Must be set before
  milestone 7 (contribution) and milestone 8 (update check).
- **Testing**: develop on Linux using the CLI, sample profiles, unit tests and GitHub
  Actions Windows runners. Checks that need real Windows hardware (real scans, UI review,
  USB flashing) are listed per milestone and batched into as few sessions as possible.

---

## 5. Milestones

Each milestone ends with tests plus CLI output against sample profiles.

| # | Milestone | Needs real Windows? |
|---|---|---|
| 1 | Scan: Windows scanner, profile JSON format, sample profiles, `greyhaven scan` | Real-scan accuracy only |
| 2 | Lookup and ratings: rules, kernel index builder, LHDB client, cache, rating engine, score, recommendation | No |
| 3 | UI: WPF shell, scan button, score header, expandable details, themes, accessibility | Yes — UI review |
| 4 | Report and export: Markdown, HTML, JSON, live-USB verification guide | No |
| 5 | Checklist and safe fixes: elevated helper, Fast Startup fix with undo | Yes — apply/undo |
| 6 | Live USB creator: download, verify, drive filtering, flash, cancel | Yes — flashing |
| 7 | Data contribution: opt-in preview, GitHub issue form, triage Action | No (needs repo name) |
| 8 | Installer and update check: WiX MSI, release workflow, notify-only update check, README | Yes — install/uninstall (needs repo name) |
