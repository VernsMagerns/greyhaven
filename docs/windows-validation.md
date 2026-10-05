# Checks that need a real Windows PC

GreyHaven is developed on Linux using sample profiles, unit tests and GitHub Actions Windows
runners. Some things can only be confirmed on real Windows hardware. They are collected here
so they can be done in as few sessions as possible. Each milestone adds its items.

**Session 1** is planned for after milestone 3 (UI), so the scanner and the UI can be
checked in the same sitting. **Session 2** is planned after milestone 6 (USB creator), and
covers safe fixes, flashing and the installer.

---

## Milestone 1: Scan

Run from a **normal (non-administrator)** PowerShell window:

```powershell
greyhaven.exe scan --dump-raw greyhaven-raw.json
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
