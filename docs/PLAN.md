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
3. **Supplements**: driver package "Modaliases" from each distro's package index (how
   NVIDIA drivers declare supported GPUs), and libfprint's supported/unsupported reader lists
   from each distro's libfprint package. (Implemented in milestone 2. `pci.ids`/`usb.ids` and
   linux-firmware were not needed so far.)
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
- Installer: WiX Toolset MSI (Start Menu shortcut, uninstaller) wrapped in a single
  `GreyHaven-Setup.exe`, built by GitHub Actions. See section 6.
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

Full definition: [docs/scoring.md](scoring.md). In short:

- Points: out of the box 100, extra driver 80, partial 40, unsupported 0.
  Unknown is excluded from the average but lowers a displayed **coverage** figure.
- Weights: critical ×3 (CPU, GPU, storage, the primary network adapter); important ×1.5
  (audio, Bluetooth, touchpad, webcam, USB, secondary network); other ×1. Chipset parts and
  external input devices are shown but not scored.
- Caps: any unsupported critical → max 40; unknown critical → max 70 ("incomplete");
  partial critical → max 79. The lowest applicable cap wins and its reason is shown.
- Bands: Great ≥90, Good ≥70, Fair ≥50, Poor <50.
- Computed per distro, rated against the kernel on the distro's install ISO. Recommendation
  order: score, fewer extra-driver steps, fewer system changes (e.g. Secure Boot off), rank.
- **(decided 2026-10-05)** When the best distro needs Secure Boot off, the user chooses an
  Easy path (Ubuntu, keeps Secure Boot on; default) or an Advanced path (best distro, with a
  Secure Boot notice and the PC maker's how-to guide). The chosen path drives fixes, the
  checklist, the default ISO and exports. Secure Boot changes are instructions-only.
- **(decided 2026-10-05)** Ratings based on approximate data are capped at medium confidence.
- **(decided 2026-10-05)** The data build date is shown everywhere; a notice appears when it's
  over 90 days old or a newer distro release exists. A weekly Action opens an issue for new releases.

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
- **GitHub owner/repo** (decided 2026-10-05): `VernsMagerns/greyhaven`, private until v1.
  Stored in one config value (`GreyHavenInfo.GitHubRepository`); `IsRepositoryPublic` keeps
  links pointing at bundled copies while private. `tools/release-check.sh` fails on placeholders.
- **Testing**: develop on Linux using the CLI, sample profiles, unit tests and GitHub
  Actions Windows runners. Checks that need real Windows hardware (real scans, UI review,
  USB flashing) are listed per milestone and batched into as few sessions as possible.

---

## 5. Milestones

UI rule (decided 2026-10-05): no "eyebrow" labels (small all-caps kicker text above
headings); lead with the heading.


Each milestone ends with tests plus CLI output against sample profiles.

| # | Milestone | Needs real Windows? |
|---|---|---|
| 1 | Scan: Windows scanner, profile JSON format, sample profiles, `greyhaven scan` | Real-scan accuracy only |
| 2 | Lookup and ratings: rules, kernel index builder, LHDB client, cache, rating engine, score, recommendation | No |
| 3 | UI: WPF shell, scan button, score header, expandable details, themes, accessibility | Yes — UI review |
| 4 | Report and export: Markdown, HTML, JSON, live-USB verification guide (done 2026-10-05) | No |
| 5 | Checklist and safe fixes: elevated helper, Fast Startup fix with undo | Yes — apply/undo |
| 6 | Live USB creator: download, verify, drive filtering, flash, cancel | Yes — flashing |
| 7 | Data contribution: opt-in preview, GitHub issue form, triage Action | No |
| 8 | Installer, release and update check: Setup.exe + MSI, release workflow, checksums, notify-only update check, README; download page and signing when public (section 6) | Yes — install/uninstall |

---

## 6. Distribution for v1 (milestone 8) — planned 2026-10-05

Goal: someone who has never heard of an `.msi` clicks one big button, double-clicks one file,
and has GreyHaven in their Start menu. Nothing below is built yet. The public parts (download
page, signing, winget) wait until the repository is public.

### Installer: `GreyHaven-Setup.exe` (primary) and the MSI (secondary)

- **MSI** (WiX v6+, built in CI): installs the self-contained app for the current machine
  (`Program Files\GreyHaven`), Start menu shortcut, Add/Remove Programs entry with a working
  uninstall, and a stable `UpgradeCode` so new versions replace old ones cleanly.
  Per-machine install needs one UAC prompt at install time; GreyHaven itself still runs
  without admin rights.
- **`GreyHaven-Setup.exe`**: a WiX **Burn** bundle wrapping the MSI, with the standard
  WiX bootstrapper UI (licence, install, progress, finish with "Launch GreyHaven"). One file,
  one double-click; it's what the download button gets. Supports `/quiet` for scripted use.
- **Recommendation: also attach the MSI to each release, but don't advertise it** on the
  download page. IT admins deploy MSIs directly (Intune, Group Policy, `msiexec /qn`) and
  some environments block bootstrapper EXEs; normal users never need to see it. Mention it
  in the README's "For IT admins" line.
- **Licensing note:** WiX 6+ has an "Open Source Maintenance Fee" that applies only to users
  with US$10,000+ annual revenue using it for revenue-generating work. A free, non-commercial
  GreyHaven is exempt. Check SignPath doesn't treat the Burn engine inside Setup.exe as a
  "proprietary component" (see Signing); if it does, Inno Setup (open source, single EXE)
  is the fallback for the wrapper, keeping the WiX MSI for admins.

### Release workflow (tag `v*` → GitHub Release)

1. Build and test on Windows; `tools/release-check.sh` must pass (no placeholders,
   `IsRepositoryPublic` set correctly).
2. Build the app, MSI and `GreyHaven-Setup.exe`; sign them (once signing is approved).
3. Write `SHA256SUMS.txt` for every downloadable file, and add GitHub artifact attestations
   (build provenance, free for public repos) so anyone can verify a file came from this repo's CI.
4. Create the release with notes: `GreyHaven-Setup.exe`, `GreyHaven.msi`, `SHA256SUMS.txt`.
   GitHub adds "Source code (zip/tar.gz)" automatically.
5. Fixed asset names (no version in the file name), so
   `https://github.com/VernsMagerns/greyhaven/releases/latest/download/GreyHaven-Setup.exe`
   always points to the newest installer. The download page and README use that link.
6. The in-app update check (notify-only) reads the latest release from the GitHub API.

### Download page (GitHub Pages)

- One static page (`site/` or `docs/site/`, deployed by a Pages workflow): what GreyHaven
  does in two sentences, one big **Download for Windows** button (the `latest/download`
  link above), size and "Windows 10 and 11, no admin rights needed to run", a short privacy
  line, and a small **View the source on GitHub** link. Same accessibility rules as the app
  (headings, contrast, keyboard, no eyebrow labels).
- **Goes live when the repository is public:** GitHub Pages for private repositories needs a
  paid plan.
- Once signing is in place, the page explains in one line what to expect from SmartScreen
  while reputation builds (see below), with a screenshot of "More info → Run anyway".

### Source access

- Every release includes the source archives (automatic), and the README gets a
  **Build from source** section: install the .NET 10 SDK, `git clone`, `dotnet test`,
  `./tools/publish-windows.sh` (or `dotnet publish` on Windows), and how to build the MSI.
- MIT for code, CC-BY-4.0 for `data/`, stated on the download page and in the installer.

### Code signing (before v1)

Unsigned installers get the strongest SmartScreen warning, aimed squarely at our audience.
Important: **no option removes the warning on day one.** Since 2024 even EV certificates
don't skip SmartScreen; reputation builds as signed releases are downloaded, and carries over
to later releases signed with the same identity. Signing still matters: an unsigned file
from an unknown publisher is treated far more harshly, and enterprises may block it outright.

Two routes, in order of preference:

1. **SignPath Foundation (free for open source).** Requirements, from signpath.org/terms:
   - OSI-approved licence for all components, no proprietary components, no malware/PUP.
     *Open question:* `data/` is CC-BY-4.0 (not an OSI licence, though it's data, not code)
     and the WiX Burn engine's licence; confirm both are acceptable when applying.
   - Actively maintained, and **already released** in the form to be signed, with the
     functionality described on the download page, and some verifiable reputation. So:
     make the repo public, publish an unsigned pre-release, then apply.
   - Named roles: authors, reviewers (approve changes from non-committers) and approvers
     (approve each signing request). One person can hold these at first.
   - **MFA** on GitHub and SignPath for everyone involved.
   - A public **code signing policy** page stating "Free code signing provided by
     SignPath.io, certificate by SignPath Foundation", listing the team per role, and a
     privacy statement. GreyHaven does send data (device IDs to linux-hardware.org when
     online lookups are on; release and update checks), so it needs a short privacy policy
     saying what is sent, when, and that lookups can be turned off.
   - Signed files must carry correct metadata (product name, version): already set in
     `Directory.Build.props`.
   - Signing runs through SignPath's GitHub Action; the certificate is in SignPath's
     name ("SignPath Foundation"), not the maintainer's.
2. **Azure Artifact Signing** (formerly Trusted Signing), if SignPath declines or is slow:
   about US$9.99/month (Basic), an Azure subscription with a payment method, and identity
   validation of the maintainer as an individual (US and Canada only) using a government ID
   (via Microsoft's verification partner) and the Microsoft Authenticator app. The
   certificate shows the maintainer's own name. Integrates with GitHub Actions; no hardware
   token.

Also worth evaluating later: the **Microsoft Store** (free developer account for individuals;
the Store signs MSIX packages itself, so no SmartScreen warning), if the elevated helper
and raw USB writing (milestones 5–6) work within MSIX's rules.

### winget (optional, after v1)

- Needs: a public, stable installer URL on GitHub Releases (yes); a silent install (MSI and
  Burn bundles are both supported installer types); manifests (YAML) submitted as a pull
  request to `microsoft/winget-pkgs`, which runs automated validation (malware scan,
  install/uninstall test); a privacy policy URL since GreyHaven sends device IDs.
- Signing isn't required but helps validation and users.
- Ongoing: a pull request per release, automated with a GitHub Action (e.g. `wingetcreate`
  or Komac) in the release workflow. Package ID, e.g. `VernsMagerns.GreyHaven`.

### Before going public (checklist)

- [ ] Decide on commit email: the history shows the maintainer's personal address; offer to
      switch to the GitHub no-reply address (rewrites history, needs a force-push).
- [ ] Set `GreyHavenInfo.IsRepositoryPublic = true`.
- [ ] Privacy policy and code signing policy pages.
- [ ] Enable MFA, branch protection on `main`, Pages.
- [ ] Revisit the Windows-jobs-on-main-only CI rule (public repos get free Actions minutes).

