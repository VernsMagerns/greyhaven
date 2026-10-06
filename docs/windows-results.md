# GreyHaven: Windows test session results

Fill this in as you go, then bring it back (along with `greyhaven-raw.json`, see step 6).
Tick a box by changing `[ ]` to `[x]`. If something is wrong, leave it unticked and write what
you saw under **Notes**. Short notes are fine ("text cut off at the right edge").

The whole session takes **about 45 minutes**. More detail on any check is in
[windows-validation.md](windows-validation.md), but you shouldn't need it.

| | |
|---|---|
| Date | |
| Windows version (Settings → System → About, "Edition" and "Version") | |
| PC maker and model, or "custom-built" | |
| Build used (first line of `START-HERE.txt`) | |

---

## Getting set up (about 10 minutes)

### Download the test build from GitHub

1. On the Windows PC, open the GreyHaven repository on GitHub in your browser and sign in.
   It's private, so you need to be signed in to see it.
2. Click the **Actions** tab near the top.
3. Click the newest run at the top of the list. It should have a green tick; if it's still
   running (yellow dot), wait a few minutes.
4. Scroll to the bottom of the run's page, to **Artifacts**, and click **greyhaven-windows**.
   Your browser downloads `greyhaven-windows.zip` (about 100 MB).

### Unzip it

5. In File Explorer, open your **Downloads** folder.
6. **Right-click** `greyhaven-windows.zip` → **Properties**. At the bottom, tick **Unblock** and
   click **OK**. This tells Windows you trust the file, so it won't warn you about each
   program inside. If there's no Unblock box, skip this step.
7. Right-click the zip again → **Extract All…** → **Extract**. A `greyhaven-windows` folder
   opens, containing `app`, `cli` and `START-HERE.txt`.

### Open a command window in that folder

8. In the `greyhaven-windows` folder, right-click an empty space:
   - **Windows 11:** choose **Open in Terminal**.
   - **Windows 10:** hold **Shift** while you right-click, then choose
     **Open PowerShell window here**.

   Use a normal window, **not** "Run as administrator".

### If Windows shows a warning

GreyHaven's test builds aren't signed, so Windows may not recognise them:

- **A blue "Windows protected your PC" box:** click **More info**, then **Run anyway**.
  This is SmartScreen, and it appears once per program.
- **"Smart App Control blocked an app":** don't change any security settings. Write it down
  here and stop; there's a workaround to plan together.

### Try the sample, then scan for real

9. Start the app in demo mode (a sample PC, no scan). Type this and press Enter:

   ```powershell
   .\app\GreyHaven.exe --sample nvidia-desktop
   ```

   Do the **UI review** checks below in demo mode first, then close the app.
10. Scan this PC and save the raw scan data:

    ```powershell
    .\cli\greyhaven.exe scan --dump-raw greyhaven-raw.json
    ```

    The file **`greyhaven-raw.json` is saved in the `greyhaven-windows` folder**, next to
    `START-HERE.txt`. That's the file to bring back.
11. Save the ratings report too (step S2 below asks you to read it):

    ```powershell
    .\cli\greyhaven.exe rate > greyhaven-report.txt
    ```

    It's saved in the same folder. Open it with `notepad greyhaven-report.txt`.
12. Finally, run `.\app\GreyHaven.exe` (no `--sample`) to see the app with your real PC.

### Bringing results back

Copy these three files to a USB stick, or email them to yourself:

- this file (`windows-results.md`), filled in
- `greyhaven-raw.json`
- `greyhaven-report.txt`

**Privacy:** the raw file holds device IDs and Windows settings only. Check S1.8 before
sharing it.

### Backup: copy the build with a USB stick instead

If the GitHub download doesn't work, build it on the Linux PC and copy it across:

1. On Linux, in the GreyHaven folder, run `./tools/publish-windows.sh`.
2. Copy the `artifacts/windows` folder to a USB stick, then from the stick to the Windows PC
   (for example, to your Desktop).
3. Carry on from step 8 above, in that copied folder. SmartScreen doesn't apply to files
   copied from a USB stick, so you won't need step 6.

---

## S1. Scan (about 10 minutes)

Compare what GreyHaven's scan (step 10) printed with what Windows says.

- [ ] **S1.1** The scan finished without errors, and without asking for administrator
      permission.
- [ ] **S1.2** Nothing important is missing or wrongly named. Compare with Device Manager
      (right-click Start → Device Manager): Wi-Fi, Bluetooth, camera, touchpad, fingerprint
      reader, sound.
- [ ] **S1.3** "Secure Boot" matches System Information: press Start, type `msinfo32`, open
      it, and look for "Secure Boot State".
- [ ] **S1.4** "Boot mode" matches "BIOS Mode" in the same System Information window.
- [ ] **S1.5** "BitLocker" matches Settings → Privacy & security → Device encryption (or
      "BitLocker" in Control Panel).
- [ ] **S1.6** "Fast Startup" matches Control Panel → Power Options → "Choose what the power
      buttons do" (the "Turn on fast startup" box).
- [ ] **S1.7** "Storage mode" matches Device Manager → Storage controllers: write down
      what's listed there.
- [ ] **S1.8** Open `greyhaven-raw.json` in Notepad. It contains no serial numbers, your
      user name, your PC's name or network (MAC) addresses.
- [ ] **S1.9** Custom-built PC only: "Motherboard" matches "BaseBoard Manufacturer" in
      System Information.

**Notes:**

&nbsp;

## S2. Ratings (about 5 minutes)

Read `greyhaven-report.txt` (step 11).

- [ ] **S2.1** Nothing looks obviously wrong for hardware you know works, or doesn't work,
      on Linux.
- [ ] **S2.2** On Windows 10, near the top there's a short, calm note about Windows 10's end
      of support. On Windows 11 there's no such note.

**Notes** (anything that looked wrong, and why you think so):

&nbsp;

## U. Look and feel (about 8 minutes)

In demo mode (`--sample nvidia-desktop`) unless it says otherwise.

- [ ] **U1** Start screen: a big "Scan my PC" button, a short explanation, a note about
      privacy, and a checkbox for online lookups. No small all-capitals labels above
      headings.
- [ ] **U2** Results, from top to bottom: the score, the recommendation, the distro
      comparison, the list of parts ("Needs your attention" first), then things to test with
      a live USB.
- [ ] **U3** The main view has no technical jargon. Device IDs, drivers and "confidence"
      only appear after clicking a part's "Technical details".
- [ ] **U4** "Easy: Ubuntu" is chosen at first. Choosing "Advanced: Pop!_OS" changes the
      score, the parts list and "You'll download", and shows a Secure Boot notice. Its
      how-to link opens MSI's Secure Boot page in your browser.
- [ ] **U5** Copy buttons copy the exact command (paste into Notepad to check).
- [ ] **U6** Source links open in your browser.
- [ ] **U7** Make the window narrow, then wide. Nothing is cut off; text wraps onto new lines.
- [ ] **U8** At 150% and 200% display scaling (Settings → System → Display → Scale), text is
      sharp and nothing overlaps. Set it back afterwards.
- [ ] **U9** Parts show an "Essential" or "Important" label, with a line under "How each
      part works on…" explaining what they mean. The two sound controllers end in "(AMD)"
      and "(NVIDIA)", and the two USB controllers end in "(1 of 2)" and "(2 of 2)".
- [ ] **U10** On Windows 10, a calm "About Windows 10" note appears at the top of the results,
      with a link to Microsoft's page.

**Notes:**

&nbsp;

## T. Light, dark and high contrast (about 4 minutes)

- [ ] **T1** With "Appearance: Match Windows", the app follows Windows' light or dark mode,
      even if you change it while GreyHaven is open (Settings → Personalization → Colors).
- [ ] **T2** Choosing "Light" or "Dark" overrides Windows. In dark mode the title bar is dark
      too. Your choice is remembered after closing and reopening the app.
- [ ] **T3** With a contrast theme on (Settings → Accessibility → Contrast themes),
      everything is readable, and ratings still show their icon and words. Turn it off
      afterwards.

**Notes:**

&nbsp;

## K. Keyboard only (about 4 minutes)

Put the mouse aside.

- [ ] **K1** The Tab key reaches every control in a sensible order.
- [ ] **K2** Space or Enter presses buttons. Arrow keys move between the Easy/Advanced choices
      and between the distro cards. Space or Enter opens and closes a part.
- [ ] **K3** Alt+S starts a scan. You can always see which control is selected (it's outlined).
- [ ] **K4** After a scan, the selection starts at "Your results".

**Notes:**

&nbsp;

## N. Screen reader (about 6 minutes)

Turn on Narrator with **Ctrl + Windows key + Enter** (same keys to turn it off).

- [ ] **N1** Every button, checkbox, choice and part is read out with a sensible name.
      Rating icons aren't read as strange symbols.
- [ ] **N2** Pressing H jumps between headings: "Your results", "What GreyHaven recommends",
      "Compare distros", "How each part works on…".
- [ ] **N3** During a scan, Narrator reads the progress ("Checking how each part works on
      Linux…").
- [ ] **N4** A part is read like "Graphics: Works after an extra step. Essential part.
      NVIDIA GeForce RTX 3070".

**Notes:**

&nbsp;

## G. Secure Boot guide links (about 3 minutes)

These makers' websites block automated checks, so a person needs to open each one. For each
link, tick it if the page opens and explains how to turn Secure Boot off.

- [ ] **G1** HP: https://support.hp.com/us-en/document/ish_6930187-6931079-16
- [ ] **G2** Lenovo (Think models): https://support.lenovo.com/solutions/HT509044
- [ ] **G3** Acer laptops: https://community.acer.com/en/kb/articles/88-enable-or-disable-secure-boot-on-an-acer-notebook
- [ ] **G4** Acer desktops: https://community.acer.com/en/kb/articles/87-enable-or-disable-secure-boot-on-an-acer-desktop
- [ ] **G5** MSI motherboards: https://www.msi.com/faq/faq-11370
- [ ] **G6** GIGABYTE motherboards (AMD): https://www.gigabyte.com/Support/Consumer/FAQ/4395

**Notes** (if a page has moved, paste the new address):

&nbsp;

---

## Anything else

Anything that surprised or confused you, even if no check covers it:

&nbsp;
