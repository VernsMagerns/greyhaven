# GreyHaven: Windows test session results

Fill this in as you go, then bring it back along with `greyhaven-raw.json` and
`greyhaven-report.txt` (see "Bringing results back").
Tick a box by changing `[ ]` to `[x]`. If something is wrong, leave it unticked and write what
you saw under **Notes**. Short notes are fine ("text cut off at the right edge").

The whole session takes **about 45 to 50 minutes**. More detail on any check is in
[windows-validation.md](windows-validation.md), but you shouldn't need it.

| | |
|---|---|
| Date | |
| Windows version (Settings → System → About, "Edition" and "Version") | |
| PC maker and model, or "custom-built" | |
| Build used (first line of `START-HERE.txt`) | |

---

## Getting set up (about 7 minutes)

### Download the test build from GitHub

1. On the Windows PC, sign in to GitHub in your browser, then open
   https://github.com/VernsMagerns/greyhaven/actions/workflows/ci.yml?query=branch%3Amain
   (the repository is private, so you need to be signed in).
2. You're now on the list of **CI** runs for `main` (the Actions tab, filtered).
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

## S1. Scan (about 7 minutes)

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

## S2. Ratings (about 4 minutes)

Read `greyhaven-report.txt` (step 11).

- [ ] **S2.1** Nothing looks obviously wrong for hardware you know works, or doesn't work,
      on Linux.
- [ ] **S2.2** On Windows 10, near the top there's a short, calm note about Windows 10's end
      of support. On Windows 11 there's no such note.
- [ ] **S2.3** The "Windows drive" line in the scan shows about the same free space as
      Settings → System → Storage.

**Notes** (anything that looked wrong, and why you think so):

&nbsp;

## U. Look and feel (about 5 minutes)

In demo mode (`--sample nvidia-desktop`) unless it says otherwise. The look is called "Market
Day": cream paper, tomato red, thick dark outlines and "sticker" shadows.

- [ ] **U1** Start screen: "Will Linux work on your PC?", a short intro, a big "Scan my PC"
      button, the online-check box, and a ticket picture on the right.
- [ ] **U2** Results, from top to bottom: a slim "Demo" strip, a headline ("Your PC is ready
      for Linux / We suggest Ubuntu") with the score in a red circle, "Why switch now?"
      (closed), "Pick your version of Linux", "What's next?", the parts, things to test,
      "Compare versions of Linux", "Save your results", and a small note at the bottom.
- [ ] **U3** It reads simply. No device IDs, drivers or other jargon unless you open
      "Show more" or "Technical details".
- [ ] **U4** Choosing Pop!_OS changes the headline ("We suggest Pop!_OS"), the score and the
      parts. Its "Show more" explains Secure Boot, links MSI's guide, and says what you'll get.
- [ ] **U5** Copy buttons copy the exact command (paste into Notepad to check).
- [ ] **U6** Links open in your browser.
- [ ] **U7** Make the window narrow, then wide, and try 150% display scaling (Settings → System
      → Display → Scale). Nothing is cut off or overlaps.
- [ ] **U8** Headings use a rounded, chunky font (Rubik) and text a clear, wide font (Lexend),
      not the usual Windows font.
- [ ] **U9** "Show more" and "Why switch now?" appear as grey bars with an arrow. Do they look
      fine, or would you prefer plain red links like the browser preview? (Write which.)

**Notes:**

&nbsp;

## W. The "What's next" walkthrough (about 6 minutes)

Click **What's next →** on the results.

- [ ] **W1** It shows "Step 1 of 9", a row of 9 bars (done green, current red), the step title
      and a "What could go wrong?" tip. Next, Back, "All steps" and "Back to results" work.
- [ ] **W2** Step 3, "Get Windows ready": only items for this PC (in demo mode: Fast Startup and
      a BIOS update link to MSI). With Pop!_OS chosen, "Turn off Secure Boot" is added.
- [ ] **W3** Step 6: in demo mode it says the startup menu key on MSI motherboards is **F11**.
      With your real PC: is the key right for your PC? (If it says "We don't know", fine.)
- [ ] **W4** Step 8: "Keep Windows too" shows your free space (real scan: compare with
      Settings → System → Storage). Choosing "Replace Windows" shows a big warning; the steps
      only appear after ticking the box. Switching away and back needs a fresh tick.
- [ ] **W5** Picture steps show dashed boxes saying "Picture coming soon" with a caption.
- [ ] **W6** Step 9 shows the graphics driver steps (on Ubuntu) and help links. "Finish" goes
      back to the results.

**Notes:**

&nbsp;

## E. Saving the report (about 3 minutes)

- [ ] **E1** "Save report…" opens a Save dialog in Documents, suggesting a name like
      `greyhaven-report-2026-10-06.html`. After saving, "Report saved" appears, and "Show in
      folder" opens File Explorer with the file selected.
- [ ] **E2** Double-click the saved file. It opens in your browser and looks like the app
      (same fonts and colours). It includes "Your steps to Linux" with all 9 steps.
- [ ] **E3** Press Ctrl+P: the print preview is readable, with the steps' boxes to tick.
- [ ] **E4** Saving as Markdown (.md) or JSON (.json) also works (pick the type in the dialog).

**Notes:**

&nbsp;

## T. Light, dark and high contrast (about 3 minutes)

- [ ] **T1** With "Appearance: Match Windows", the app follows Windows' light or dark mode,
      even if you change it while GreyHaven is open (Settings → Personalization → Colors).
- [ ] **T2** "Dark" is a very dark slate blue-grey with light text; the title bar is dark too.
      Your choice is remembered after closing and reopening the app.
- [ ] **T3** With a contrast theme on (Settings → Accessibility → Contrast themes), everything is
      readable, the ticket picture and shadows disappear, and ratings still show their words.
      Turn it off afterwards.

**Notes:**

&nbsp;

## K. Keyboard only (about 3 minutes)

Put the mouse aside.

- [ ] **K1** Tab reaches every control in a sensible order, including "Show more" and the
      walkthrough buttons. You can always see which control is selected.
- [ ] **K2** Space or Enter presses buttons and opens "Show more". Arrow keys move between the
      version choices, the distro cards and the install choices.
- [ ] **K3** Shortcuts: Alt+S scans, Alt+N opens "What's next", Alt+R saves the report.
- [ ] **K4** After a scan, the selection starts at the headline. On each new step, it starts at
      the step's title.

**Notes:**

&nbsp;

## N. Screen reader (about 4 minutes)

Turn on Narrator with **Ctrl + Windows key + Enter** (same keys to turn it off).

- [ ] **N1** Every button, checkbox, choice and "Show more" is read with a sensible name, like
      "Show more about Graphics: Needs one extra step. Essential part. NVIDIA GeForce RTX 3070".
- [ ] **N2** Pressing H jumps between headings ("Your PC is ready for Linux", "Pick your version
      of Linux", "What's next?", "Your PC's parts on Ubuntu"...).
- [ ] **N3** During a scan, Narrator reads the progress. The score is read as "Score: 98 out
      of 100".
- [ ] **N4** In the walkthrough, each new step is read like "Step 3 of 9: Get Windows ready".
      The row of progress bars is not read out.

**Notes:**

&nbsp;

## G. Links to click (about 3 minutes)

These makers' websites block automated checks, so a person needs to open each one. Tick it
if the page opens and covers what it says.

Secure Boot guides (explain how to turn Secure Boot off):

- [ ] **G1** HP: https://support.hp.com/us-en/document/ish_6930187-6931079-16
- [ ] **G2** Lenovo (Think models): https://support.lenovo.com/solutions/HT509044
- [ ] **G3** Acer laptops: https://community.acer.com/en/kb/articles/88-enable-or-disable-secure-boot-on-an-acer-notebook
- [ ] **G4** Acer desktops: https://community.acer.com/en/kb/articles/87-enable-or-disable-secure-boot-on-an-acer-desktop
- [ ] **G5** MSI motherboards: https://www.msi.com/faq/faq-11370
- [ ] **G6** GIGABYTE motherboards (AMD): https://www.gigabyte.com/Support/Consumer/FAQ/4395

Support pages (where to find BIOS updates):

- [ ] **G7** Dell: https://www.dell.com/support/home
- [ ] **G8** MSI: https://www.msi.com/support
- [ ] **G9** GIGABYTE: https://www.gigabyte.com/Support
- [ ] **G10** Acer: https://www.acer.com/support
- [ ] **G11** Microsoft Surface: https://support.microsoft.com/surface

Startup menu keys only found on other websites. If you own one of these, does the key work?
Lenovo F12 (Novo button on some IdeaPads), ASUS Esc on laptops and F8 on desktops,
GIGABYTE F12.

**Notes** (if a page has moved, paste the new address):

&nbsp;

---

## Anything else

Anything that surprised or confused you, even if no check covers it:

&nbsp;
