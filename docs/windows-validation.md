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

## UI review (milestone 3)

Start with demo mode, which shows a PC with two recommendation paths:
`app\GreyHaven.exe --sample nvidia-desktop`. Then run `app\GreyHaven.exe` for your real PC.

Look and feel
- [ ] **U1** Start screen: big "Scan my PC" button, short explanation, privacy text, online
      lookups checkbox. No small all-caps labels above headings.
- [ ] **U2** Results: the score is at the top, then the recommendation, the distro comparison,
      the parts list ("Needs your attention" first) and the live-USB tests.
- [ ] **U3** The main view has no jargon. Device IDs, drivers and confidence only appear
      under each part's "Technical details".
- [ ] **U4** Recommendation paths (demo mode): "Easy: Ubuntu" is selected by default.
      Choosing "Advanced: Pop!_OS" changes the score, the parts list and "You'll download",
      and shows the Secure Boot notice. Its guide link opens MSI's Secure Boot page (the
      sample is a custom-built PC with an MSI motherboard).
- [ ] **U5** Copy buttons put the exact command on the clipboard (paste into Notepad).
- [ ] **U6** Source links open in the browser.
- [ ] **U7** Resize the window narrow and wide. Nothing is cut off; text wraps.
- [ ] **U8** At 150% and 200% display scaling (Settings → System → Display → Scale),
      text is sharp and nothing overlaps.
- [ ] **U9** Parts carry "Essential" or "Important" labels, explained under the parts heading.
      Same-named parts are told apart: sound controllers "(AMD)"/"(NVIDIA)", USB controllers
      "(1 of 2)"/"(2 of 2)".
- [ ] **U10** On Windows 10, a calm "About Windows 10" notice with a link to Microsoft's page.

Saving the report (milestone 4)
- [ ] **E1** "Save report…" opens a Save dialog in Documents, suggesting
      `greyhaven-report-<date>.html`. Saving shows "Report saved", and "Show in folder" opens
      File Explorer with the file selected.
- [ ] **E2** The saved web page opens in Edge or another browser, looks right in light and
      dark mode, and prints legibly (Ctrl+P preview). It follows the path you chose.
- [ ] **E3** Saving as Markdown (.md) or JSON (.json) also works (choose the type in the dialog).

Themes
- [ ] **T1** "Appearance: Match Windows" follows Windows' light/dark app mode, including when
      you change it while GreyHaven is open.
- [ ] **T2** "Light" and "Dark" override it. The title bar matches in dark mode. The choice is
      remembered after restarting the app.
- [ ] **T3** Turn on a High Contrast theme (Settings → Accessibility → Contrast themes).
      Everything is readable, and ratings still show their icon and text.

Keyboard (unplug or ignore the mouse)
- [ ] **K1** Tab reaches every control in a sensible order: appearance box, scan button,
      checkbox; on results, the path choices, distro cards, each part, the links and copy
      buttons inside an expanded part, and "Scan again".
- [ ] **K2** Space/Enter activates buttons; arrow keys move between path choices and
      between distro cards; Space/Enter expands and collapses a part.
- [ ] **K3** Alt+S starts a scan (access key). The focused control is always visibly
      highlighted.
- [ ] **K4** After a scan, keyboard focus lands on "Your results".

Screen reader (Narrator: Ctrl+Win+Enter)
- [ ] **N1** Every button, checkbox, radio button and expander is announced with a meaningful
      name. Rating icons are not read as strange characters.
- [ ] **N2** Headings can be navigated (Narrator: H / Shift+H): "Your results", "What GreyHaven
      recommends", "Compare distros", "How each part works on ...".
- [ ] **N3** Progress during a scan ("Checking how each part works on Linux...") is announced.
- [ ] **N4** A part's expander is announced like "Graphics: Works after an extra step.
      Essential part. NVIDIA GeForce RTX 3070".

## Secure Boot guide links (any PC, any browser)

These official pages block automated checks, so a person needs to confirm each one opens and
explains how to turn Secure Boot off. Update `data/secure-boot-guides.yaml` if one has moved.

- [ ] **G.1** HP: https://support.hp.com/us-en/document/ish_6930187-6931079-16
- [ ] **G.2** Lenovo (Think models): https://support.lenovo.com/solutions/HT509044
- [ ] **G.3** Acer notebook: https://community.acer.com/en/kb/articles/88-enable-or-disable-secure-boot-on-an-acer-notebook
- [ ] **G.4** Acer desktop: https://community.acer.com/en/kb/articles/87-enable-or-disable-secure-boot-on-an-acer-desktop
- [ ] **G.5** MSI motherboards: https://www.msi.com/faq/faq-11370
- [ ] **G.6** GIGABYTE AMD motherboards: https://www.gigabyte.com/Support/Consumer/FAQ/4395

---

# Session 2

Added by milestones 5 (safe fixes), 6 (USB creator) and 8 (installer).
