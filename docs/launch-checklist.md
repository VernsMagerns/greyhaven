# Launch checklist: GreyHaven v1 for Windows

Everything left before GreyHaven goes public, kept up to date as we go. Last updated
9 October 2026 (Pacific).

**Status:** ✅ done · 🔨 needs building (Claude) · 🧪 needs testing (on Windows) · 👤 only you
can do it.

## Shortest path to launch

1. **👤 Windows session 2, then your own switch.** [windows-results.md](windows-results.md)
   ends with what to watch for during your install.
2. **🔨 While you do that, I build what's left that doesn't need Windows**, in this order:
   - the installer (`GreyHaven-Setup.exe` and the MSI)
   - the update check
   - the release workflow
   - the download page
   - the user README
3. **🔨 I fix whatever session 2 and your switch turn up.**
4. **🧪 One VM session for everything else.** One Windows 10 VM and one Windows 11 Pro VM with
   BitLocker on. Each runs as an admin and as a standard user, and the installer is tested
   there too (install, upgrade, uninstall). About 2 to 3 hours, mostly waiting for VMs to install.
5. **👤 Going-public steps 1, 2, 3 and 6** (contact address, no-reply email, MFA, branch
   rule). Any time before step 6 of this list, in parallel with the rest.
6. **👤 + 🔨 Go public.** You make the repository public and turn on Pages; I publish v1.0.0
   (unsigned).
7. **After launch:** apply to SignPath, then signed releases from v1.0.1.

The contribute-devices feature is left out of v1 (see section 5). The biggest unknowns are
session 2 and the BitLocker VM: both test code that only runs on real Windows.

---

## 1. Your switch and session 2

| | Item | Status |
|---|---|---|
| 1.1 | Windows session 2: real USB writing (on an external SSD), permission prompts, restart into the stick, Try Ubuntu | ✅ 9 Oct, done as part of your switch. The form itself wasn't filled in. |
| 1.2 | Your real switch: install Ubuntu with Keep Windows too, following the phone guide | ✅ 9 Oct: "It worked perfectly." |
| 1.3 | Fix what 1.1 and 1.2 find: external SSDs over 256 GB were refused; now allowed with a second "not my backup" tick | ✅ 9 Oct |
| 1.4 | Replay your fresh `greyhaven-raw.json` (with apps); fill gaps in the app list from sources. Your real app list never goes into git | 🔨 after you report |
| 1.5 | Guide checked against Ubuntu 26.04's installer (tutorial and the installer's own text, 26.04.1); fixes for the divider wording, the "remove the installation medium" prompt, and NVIDIA plus Secure Boot | ✅ 8 Oct |
| 1.6 | Real-flow hardening: wipe the end of the stick before writing; check free space before the 6 GB download; delete the download after the stick is checked | ✅ 8 Oct |

## 2. Windows 10 and BitLocker (VMs)

How to set up the VMs: [windows-versions.md](windows-versions.md). BitLocker needs a virtual
TPM: in virt-manager, add a **TPM** device (emulated, version 2.0) and use UEFI firmware with
Secure Boot.

| | Item | Status |
|---|---|---|
| 2.1 | **Windows 10 22H2 VM:** scan, results, walkthrough to the restart screen, saved report. Icons fall back correctly. Fast Startup's one-click fix and undo. | 🧪 |
| 2.2 | **Windows 11 Pro VM with BitLocker on** (`manage-bde -on C:` or Control Panel), **required**: (a) the scan says the drive is encrypted; (b) Keep Windows is blocked until encryption is off, with the reason shown; (c) the one-click **Turn off encryption** works through the permission prompt and the screen updates when it finishes; (d) Replace Windows says clearly what happens to the encrypted drive. | 🧪 |
| 2.3 | Windows 11 **Home** "Device encryption" can't usually be switched on in a VM (it needs specific hardware). The scan reads the same Windows setting as BitLocker, so 2.2 covers the detection. The Home wording (Settings > Device encryption) is checked by eye only. | 🧪 (by eye) |
| 2.4 | **Standard (non-admin) account** in either VM. When Windows asks for an administrator's password, the helper runs as a different user. Its private connection is limited to the same user, so it would likely fail with "didn't answer". Test it; if it fails, I change how the helper connects. | 🧪 then 🔨 |
| 2.5 | Display scaling at 150% and Narrator on the new screens (game search, pros and cons pop-up, sound toggle) | 🧪 |

## 3. Installer, update check and releases

None of this is built yet (plan: [PLAN.md](PLAN.md) section 6).

| | Item | Status |
|---|---|---|
| 3.1 | MSI (WiX): Program Files, Start menu entry, uninstall entry, upgrades replace old versions. The helper ships next to the app. | 🔨 |
| 3.2 | `GreyHaven-Setup.exe` (WiX Burn wrapper around the MSI), with a "Launch GreyHaven" finish | 🔨 |
| 3.3 | Uninstall also offers to remove `%LOCALAPPDATA%\GreyHaven` (settings, download cache). It leaves your saved guide in Documents. | 🔨 |
| 3.4 | Install, upgrade (0.9 → 1.0) and uninstall on Windows 10 and 11 (in the step 4 VM session) | 🧪 |
| 3.5 | Update check: notify-only, reads the latest release from GitHub, never downloads by itself, can be turned off. Only works once the repository is public. | 🔨 then 🧪 |
| 3.6 | Release workflow: a `v*` tag builds, tests and creates the release with Setup.exe, the MSI and `SHA256SUMS.txt`, under fixed file names | 🔨 |
| 3.7 | Build attestations (proof a file came from this repository's CI). Free once the repository is public. | 🔨 after going public |
| 3.8 | Dry run: a `v0.9.0-rc.1` pre-release, downloaded and installed in a VM | 🧪 |
| 3.9 | What SmartScreen and Defender do with the unsigned Setup.exe, written down with screenshots for the download page | 🧪 |
| 3.10 | Version 1.0.0 and release notes | 🔨 (you approve the notes) |

## 4. Website and documents

| | Item | Status |
|---|---|---|
| 4.1 | Download page (GitHub Pages): what it does, one Download button, privacy line, source link | 🔨, goes live after going public |
| 4.2 | README for users (it still says "not ready for users"), plus **Build from source** and a line for IT admins | 🔨 |
| 4.3 | PRIVACY.md: up to date (phone guide, apps, game search) | ✅ (update check line added with 3.5) |
| 4.4 | Phone guide site (Cloudflare Pages) | ✅ live |
| 4.5 | Fresh data just before release: ProtonDB dump, anti-cheat list, distro versions | 🔨 |
| 4.6 | Windows on Arm: the x64 build runs under emulation there, but USB writing hasn't been tested. Either say "not tested yet" on the download page, or test on an Arm laptop. | 👤 decide |

## 5. "Contribute unknown devices": wait until after v1

**Recommendation: leave it out of v1.** Reasons:

- What it would collect already flows another way. Step 1 of the guide ("Optional: share your
  results to help others") runs `hw-probe`, which uploads to linux-hardware.org, and GreyHaven
  already reads that database.
- Every report would become a public GitHub issue that someone has to check and turn into a
  data change. At launch, that someone is you.
- It needs the triage workflow, issue templates and a public repository, so it would slow the
  launch.

If you want something small in v1, one "Report a part GreyHaven didn't know" link (a
pre-filled issue you review in the browser) is about a day's work. **Status: 👤 decide**
(recommended: wait).

## 6. Going public ([going-public.md](going-public.md))

| | Step | Status |
|---|---|---|
| 6.1 | Pick a contact address for conduct reports | 👤 |
| 6.2 | Switch commit email to GitHub's no-reply address. You copy it; I rewrite history and force-push, only after you confirm. `test.wav` (committed by accident, now untracked) can be dropped from history in the same rewrite. | 👤 then 🔨 |
| 6.3 | Two-factor authentication on GitHub | 👤 |
| 6.4 | Installer and release workflow (section 3) | 🔨 |
| 6.5 | `IsRepositoryPublic = true`, contact address in place, `release-check.sh` passes | 🔨 |
| 6.6 | Private vulnerability reporting on; branch rule for `main` | 👤 |
| 6.7 | Make the repository public | 👤 |
| 6.8 | Turn on GitHub Pages (Source: GitHub Actions) | 👤 |
| 6.9 | First public release, unsigned (v1.0.0) | 🔨 (you approve) |
| 6.10 | CI: Windows jobs on every pull request again (free minutes for public repositories) | 🔨 |

## 7. Code signing (after going public)

| | Step | Status |
|---|---|---|
| 7.1 | SignPath account with two-factor authentication | 👤 |
| 7.2 | Apply to SignPath Foundation ([signpath-application.md](signpath-application.md)) | 👤 |
| 7.3 | Signing in the release workflow (you approve each signing request) | 🔨 after approval |
| 7.4 | Code signing policy page, linked from the download page and README | 🔨 |
| 7.5 | If SignPath says no: Azure Artifact Signing (about US$9.99 a month, ID check) | 👤 decide |

## 8. Already done

The scan, ratings, results, apps, Check a game, and the five-part walkthrough. Also the signed
download check, USB writing through the permission-prompt helper, restart into the stick,
the phone guide with QR code, the saved report, accessibility and High Contrast, sounds, and
CI on Linux and Windows. Windows session 1 is done (7 October).
