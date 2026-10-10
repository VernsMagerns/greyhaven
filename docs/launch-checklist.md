# Launch checklist: GreyHaven v1 for Windows

Everything left before GreyHaven goes public, kept up to date as we go. Last updated
9 October 2026 (Pacific), evening.

**Status:** ✅ done · 🔨 needs building (Claude) · 🧪 needs testing (on Windows) · 👤 only you
can do it.

## Shortest path to launch

**Step-by-step for everything left: [final-session.md](final-session.md).**

1. ~~👤 Windows session 2, then your own switch~~: done 9 October. It worked.
2. ~~🔨 Build what's left that doesn't need Windows~~: done 9 October. That's the installer
   (built and install-tested in CI), the update check, the release workflow, the download page,
   the README with screenshots, and the brand.
3. ~~🔨 Fix what session 2 and your switch turned up~~: done (external SSDs).
4. **🧪 One VM session for everything else.** One Windows 10 VM and one Windows 11 Pro VM with
   BitLocker on. Each runs as an admin and as a standard user, and the installer is tested
   there too (install, upgrade, uninstall). About 2 to 3 hours, mostly waiting for VMs to install.
5. **👤 Going-public steps 1, 2, 3 and 6** (contact address, no-reply email, MFA, branch
   rule). Any time before step 6 of this list, in parallel with the rest.
6. **👤 + 🔨 Go public.** You make the repository public and turn on Pages; I publish v1.0.0
   (unsigned).
7. **After launch:** apply to SignPath, then signed releases from v1.0.1.

The contribute-devices feature is left out of v1 (see section 5). The biggest unknown left is
the BitLocker VM: that code only runs on real Windows and hasn't met an encrypted drive yet.

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
| 2.4 | **Standard (non-admin) account:** the helper's connection now also accepts administrators (fixed 10 Oct; CI checks the normal handshake on every push). Confirm with a real standard account. | 🧪 (final-session.md B3) |
| 2.5 | Display scaling at 150% and Narrator on the new screens (game search, pros and cons pop-up, sound toggle) | 🧪 |

## 3. Installer, update check and releases

None of this is built yet (plan: [PLAN.md](PLAN.md) section 6).

| | Item | Status |
|---|---|---|
| 3.1 | MSI (WiX): Program Files, Start menu entry, uninstall entry, upgrades replace old versions. The helper ships next to the app. | ✅ 9 Oct. Built in CI, then installed, checked and uninstalled on a Windows runner on every push to main. |
| 3.2 | `GreyHaven-Setup.exe` (WiX Burn wrapper around the MSI), with a "Launch GreyHaven" finish | ✅ 9 Oct. Quiet install and uninstall tested in CI; it's on the latest-test release. The screens themselves still need a look by eye (3.4). |
| 3.3 | Uninstall leaves `%LOCALAPPDATA%\GreyHaven` (settings, plus a half-finished download if one was cancelled). Clean it up, or document it. It leaves your saved guide in Documents. | 🔨 (small) |
| 3.4 | Install, upgrade (0.9 → 1.0) and uninstall on Windows 10 and 11 (in the step 4 VM session) | 🧪 |
| 3.5 | Update check: notify-only, reads the latest release from GitHub, never downloads by itself, can be turned off. Only works once the repository is public. | ✅ built 9 Oct; 🧪 after the first two public releases |
| 3.6 | Release workflow: a `v*` tag builds, tests, installs and uninstalls once more, then creates a **draft** release with Setup.exe, the MSI, a portable zip and `SHA256SUMS.txt`, under fixed file names | ✅ 9 Oct (not run yet) |
| 3.7 | Build attestations (proof a file came from this repository's CI). Free once the repository is public. | ✅ in the release workflow; switches on by itself when public |
| 3.8 | Dry run: tag `v0.9.0`. That creates a draft release, which only you can see. Download it and install it in a VM, then delete the draft and the tag. | 🧪 (I push the tag when you say) |
| 3.9 | What SmartScreen and Defender do with the unsigned Setup.exe, written down with screenshots for the download page | 🧪 |
| 3.10 | Version 1.0.0 and release notes | 🔨 (you approve the notes) |

## 4. Website and documents

| | Item | Status |
|---|---|---|
| 4.1 | Download page (`site/`): what it does, one Download button, how it works, is it safe, questions, in the brand style | ✅ built 9 Oct; goes live when public (Settings > Pages > GitHub Actions) |
| 4.2 | README for users, with real screenshots from CI, safety and questions sections; developer notes in [developing.md](developing.md) | ✅ 9 Oct |
| 4.3 | PRIVACY.md: up to date (phone guide, apps, game search) | ✅ (update check line added with 3.5) |
| 4.4 | Phone guide site (Cloudflare Pages) | ✅ live |
| 4.5 | Fresh data just before release: ProtonDB dump, anti-cheat list, distro versions | 🔨 |
| 4.7 | Brand: the lighthouse mark, app icon, banners, social preview, [brand.md](brand.md) | ✅ 9 Oct |
| 4.8 | Repository description and topics | ✅ 9 Oct |
| 4.9 | Social preview image: upload `assets/brand/social-preview.png` in Settings → General → Social preview. The setting only appears once the repository is public. | 👤 at going public |
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
| 6.2 | Commit email switched to GitHub's no-reply address in all history; test.wav dropped from history | ✅ 10 Oct |
| 6.3 | Two-factor authentication on GitHub | ✅ 10 Oct |
| 6.4 | Installer and release workflow (section 3) | 🔨 |
| 6.5 | `IsRepositoryPublic = true`, contact address in place, `release-check.sh` passes | 🔨 |
| 6.6 | Branch rule for `main`: set up 10 Oct, but GitHub Free only enforces it on public repositories. Private vulnerability reporting is public-only too. Check both with 6.8. | 👤 at going public |
| 6.7 | Make the repository public | 👤 |
| 6.8 | Right after going public: Pages (Source: GitHub Actions), social preview, private vulnerability reporting, and check the `main` branch rule shows as active | 👤 |
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
