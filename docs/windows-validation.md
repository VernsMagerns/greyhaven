# Checks that need a real Windows PC

GreyHaven is developed on Linux using sample profiles, unit tests and GitHub Actions Windows
runners. Some things can only be confirmed on real Windows hardware. They are collected here
so they can be done in as few sessions as possible.

- **Session 1** (after milestone 3): scan accuracy, ratings and the UI review. About 45 minutes.
- **Session 2** (after milestone 6): safe fixes, USB flashing and the installer.

---

# Session 1

## Before you start

Step-by-step setup and a results form to fill in are in
[windows-results.md](windows-results.md). In short: download the **greyhaven-windows** build
from the latest GitHub Actions run (or, as a backup, run `./tools/publish-windows.sh` on Linux
and copy `artifacts/windows` across on a USB stick). Nothing needs installing; the apps run
from the folder. Use a **normal (non-administrator)** account or PowerShell window throughout.

If you can, test on both **Windows 10 and Windows 11**; if only one is available, note which.

## Scan (milestone 1)

In PowerShell, in the copied folder:

```powershell
cli\greyhaven.exe scan --dump-raw greyhaven-raw.json
```

- [ ] **S1.1** The scan finishes without errors and without a UAC prompt.
- [ ] **S1.2** Compare the component list with Device Manager. Is anything important missing
      (Wi-Fi, Bluetooth, camera, touchpad, fingerprint, sound)? Is anything mislabelled?
- [ ] **S1.3** Secure Boot matches `msinfo32` → System Summary → "Secure Boot State".
- [ ] **S1.4** Boot mode matches `msinfo32` → "BIOS Mode".
- [ ] **S1.5** BitLocker status matches Settings → Privacy & security → Device encryption,
      or `manage-bde -status C:` in an **admin** prompt. The mapping of the shell's BitLocker
      value is based on community documentation; this check confirms it.
- [ ] **S1.6** Fast Startup matches Control Panel → Power Options → "Choose what the power
      buttons do".
- [ ] **S1.7** Storage mode: in Device Manager → Storage controllers, note whether you see
      "Standard SATA AHCI Controller", "Standard NVM Express Controller" or
      "Intel RST VMD Controller". Compare with GreyHaven's "Storage mode".
- [ ] **S1.8** Open `greyhaven-raw.json` and confirm it contains no serial numbers, your
      user name, your PC's name or MAC addresses. Send it to the project if anything looked
      wrong in S1.2–S1.7; it can be replayed on Linux with `greyhaven scan --raw <file>`.
- [ ] **S1.9** On a custom-built PC: the scan's "Motherboard" line matches the maker printed
      on the board or shown in `msinfo32` → "BaseBoard Manufacturer".

## Ratings (milestone 2)

- [ ] **S2.1** Run `cli\greyhaven.exe rate` and skim the report. Does anything look obviously
      wrong for hardware you know works or doesn't work on Linux? Note it; it becomes a rule
      or a fix.
- [ ] **S2.2** On Windows 10, the report starts with a short, calm note about Windows 10's end of
      support. On Windows 11 there is no such note.
- [ ] **S2.3** The scan's "Windows drive" free space matches Settings → System → Storage.

## UI, walkthrough and report (milestones 3-4, redesigned 2026-10-06)

The checks for the "Market Day" redesign live in one place, the form you fill in:
[windows-results.md](windows-results.md), sections **U** (look and feel), **W** (the "What's
next" walkthrough), **E** (saving the report), **T** (themes and high contrast), **K**
(keyboard), **N** (Narrator) and **G** (links and boot keys to confirm by hand).

---

# Session 2

Added by milestones 5 (safe fixes), 6 (USB creator) and 8 (installer). Test on both Windows 10
and Windows 11 (one can be a VM, see [windows-versions.md](windows-versions.md)), especially
installing, the Start menu entry and uninstalling.
