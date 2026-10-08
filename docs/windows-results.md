# GreyHaven: Windows test session 2 (Part D)

Fill this in as you go, then bring it back with a fresh `greyhaven-raw.json` (see the end).
Tick a box by changing `[ ]` to `[x]`. If something is wrong, leave it unticked and write what
you saw under **Notes**. Short notes are fine.

**What this session covers:** the new results screen with your apps, and the whole new
walkthrough on your own PC: choosing how to install, getting Windows ready, **making a real USB
stick**, taking the guide to your phone, **restarting into the stick and trying Linux**. It
stops before installing anything: Windows and your files aren't changed.

**You'll need:** your PC, your phone, and a **spare USB stick of 8 GB or more whose contents you
don't need** (it will be erased). Time: about **1 to 1½ hours**, most of it the 6 GB download.

(Session 1, 7 October 2026: real PC, Windows 11, no errors, no admin needed, score 98, Ubuntu
suggested. Its findings became Parts A to D. The old form is in the repository's history.)

| | |
|---|---|
| Date | |
| Build used (the commit in the release title, also the first line of `START-HERE.txt`) | |
| Spare USB stick (brand and size) | |

---

## 1. Get the new test build (about 5 minutes)

1. Make sure the newest build is out: open
   https://github.com/VernsMagerns/greyhaven/releases/tag/latest-test (signed in to GitHub).
   The title should say a commit **newer than `47e8843`**. If it doesn't, wait a few minutes:
   GitHub builds it after each change.
2. Download https://github.com/VernsMagerns/greyhaven/releases/download/latest-test/greyhaven-windows.zip
3. In **Downloads**, right-click the zip → **Properties** → tick **Unblock** → **OK**, then
   right-click → **Extract All…** → **Extract**. (Delete the old `greyhaven-windows` folder
   first if it's still there.)
4. In the new `greyhaven-windows` folder, right-click an empty space → **Open in Terminal**.
   A normal window, **not** "Run as administrator".
5. Check the helper is there: the `app` folder should contain **`GreyHaven.Helper.exe`** next
   to `GreyHaven.exe`.

- [ ] Got the build, and `GreyHaven.Helper.exe` is in the `app` folder.

**Notes:**

## 2. Fresh scan with your apps (about 3 minutes)

```powershell
.\cli\greyhaven.exe scan --dump-raw greyhaven-raw.json
```

This saves `greyhaven-raw.json` in the `greyhaven-windows` folder. **It now includes the names
of your installed apps** (no folders, files or versions; your Windows user name is replaced
with `<user>`). It stays private: it's never committed to git.

- [ ] The scan finished without errors.
- [ ] I opened `greyhaven-raw.json` in Notepad and searched for `"apps"`: there's a list of my
      apps, with nothing I'd consider private (if there is, note which app below, without
      the private bit).

**Notes:**

## 3. The results screen, on your PC (about 10 minutes)

Start the app on your real PC (not demo mode):

```powershell
.\app\GreyHaven.exe
```

Click **Scan my PC**.

- [ ] Right under the headline there's a big **Show me how to switch →** button, easy to find.
- [ ] The parts are calm: no "Not sure" on the NVIDIA Virtual Audio Device, the two "AMD PCI"
      parts, the NVIDIA USB Type-C controller or the Scarlett Solo (the first three shouldn't
      be listed at all; the Scarlett says it works).
- [ ] **System chipset: N parts, all fine** is one line, and it's not counted in the
      "parts work right away" sentence.
- [ ] "Show more" and "Why switch now?" (if shown) look like red text links with an arrow;
      Tab reaches them with a visible outline.
- [ ] **Your apps** shows a summary line like "N apps work. N have a website or alternative…".
- [ ] Open **Show your N apps**. The groups make sense, and the ratings for apps you know look
      right. Note any that look wrong:
- [ ] Tick **I need this app** on an app that won't work (a game with anti-cheat, if you have
      one). A warning appears under it **and** under the headline.
- [ ] **Save report…** (leave "Include my apps" unticked) → open the file → press Ctrl+F and
      search for one of your app names: **not found**. Save again **with** "Include my apps"
      ticked: now it's there, under "Your apps".

**Apps that look wrong, or missing apps you'd expect:**

**Notes:**

## 4. The walkthrough, part 1: Get ready (about 10 minutes)

Click **Show me how to switch →**.

- [ ] Five parts across the top: Get ready, Make your USB stick, Take your guide, Restart, On
      the USB stick. "Get ready" is highlighted.
- [ ] **How do you want to install Linux?** "Keep Windows too" is picked and marked Suggested,
      with ✓ lines for free space (about right for your C: drive?) and encryption.
- [ ] If you ticked a must-have app in step 3: Keep Windows says you can still use it, and
      Replace Windows warns.
- [ ] Pick **Replace Windows**, then **Next**: Back up says it's **required**, and **Next**
      stays greyed out until every item is ticked. Go **Back** and pick **Keep Windows too**
      again (that's what the rest of this session uses).
- [ ] **Get Windows ready:** Fast Startup says **Already done on your PC** (yours is off).

**Optional, to test the permission prompt (3 minutes):** turn Fast Startup *on* so GreyHaven
can turn it off: Control Panel → Power Options → "Choose what the power buttons do" → "Change
settings that are currently unavailable" → tick "Turn on fast startup" → Save changes. Close
GreyHaven, start it again, scan, and go back to **Get Windows ready**:

- [ ] There's a **Turn off Fast Startup** button. Clicking it first shows what will change and
      the line "Windows will ask for permission so GreyHaven can change the Fast Startup
      setting.", with **Yes, do it** and **Cancel**.
- [ ] **Yes, do it** → Windows' permission prompt names **GreyHaven.Helper** → **Yes** → "✓ Done."
- [ ] **Undo** turns it back on (another prompt), and doing it again turns it off.
- [ ] Clicking **No** on the permission prompt says nothing was changed.
- [ ] Afterwards, Control Panel shows Fast Startup **off**.

**Notes:**

## 5. Part 2: Make your USB stick (about 20 to 40 minutes, mostly waiting)

**This erases the spare stick.** Unplug any other USB sticks and external drives first, so
there's no doubt which one is used.

- [ ] Before plugging it in: the screen says "Looking for USB sticks… Plug one in.", and your
      PC's own drives are **not** listed.
- [ ] Plug in the spare stick: within a few seconds it appears with its name and size, and
      it's selected.
- [ ] Unplug it: it disappears. Plug it back in: it's back.
- [ ] **Make my USB stick** stays greyed out until you tick "I understand everything on the
      … stick will be erased", which names the stick.
- [ ] Tick it and click **Make my USB stick**. It shows "Finding the official download", then
      **Downloading** with a progress bar.
- [ ] **Cancel** during the download: it says "Stopped. Nothing on your PC changed." Click
      **Try again**, tick the box again, and start again: the download **continues** from where
      it stopped (the bar starts part-way).
- [ ] After the download: **Checking the file**, then the line "Windows will ask for permission
      so GreyHaven can write to your USB stick." just before Windows' permission prompt.
- [ ] **Yes** → **Writing to the stick**, then **Checking the stick**, then **✓ Your USB stick
      is ready.**
- [ ] If Windows pops up "You need to format the disk", click **Cancel** (that's expected).
- [ ] **Next** is now available.

How long did it take? Download: ___ min. Writing and checking: ___ min.

**Notes:**

## 6. Part 3: Take your guide (about 5 minutes)

- [ ] A QR code is shown, and "Saved on your Windows drive: …\Documents\GreyHaven\GreyHaven guide.html".
- [ ] **Turn Wi-Fi off on your phone** and scan the code: the guide opens over mobile data.
- [ ] On the phone: "PC didn't start from the USB stick?" says **F11**; step 2 suggests about
      the right number of GB; step 3 shows the **NVIDIA** steps; there's **no touchpad** check
      (it's a desktop); "Install your apps" only says where your list is.
- [ ] Tap a picture: it fills the screen; **Close picture** (or tapping outside, or Back)
      returns.
- [ ] **Open the guide to print or save it** opens your copy in the browser. It has your
      **Install your apps** checklist (with where to get each app on Ubuntu).

**Notes:**

## 7. Part 4: Restart into the stick, and try Linux (about 15 minutes)

Save anything open in other apps first. Keep your phone with the guide open.

- [ ] **Restart into your USB stick** is greyed out until all the checks are ticked.
- [ ] Tick them and click it. The PC restarts **without asking for an administrator password**
      and shows Windows' blue **Choose an option** screen.
- [ ] Click **Use a device**. Is the USB stick listed? What's it called?
      Name shown: ______
- [ ] Click it: the PC starts from the stick. (A menu may appear; the first choice is fine.)

If the stick wasn't listed, or Windows started instead: restart, press **F11** again and again
as the PC starts, and pick the stick. Note what happened:

- [ ] Ubuntu starts. Following the phone guide, choose **Try Ubuntu**.
- [ ] Try a few checks from the guide: the screen, wired network (open a website), sound
      through your **Scarlett Solo** and through your monitor if you use it, sleep and wake.
- [ ] On the desktop, what is the installer's icon called? (The guide says its name starts
      with "Install".) Name: ______
- [ ] **Don't install.** Shut down from the power menu, unplug the stick, start the PC:
      Windows starts as before.

**Notes:**

## 8. Quick checks (about 5 minutes)

- [ ] In GreyHaven's walkthrough, use **Tab** to reach a picture and press **Enter**: it
      enlarges; **Escape** closes it and the picture has focus again.
- [ ] Appearance → **Dark**: the walkthrough and apps section look right.
- [ ] Optional: restart and press **Delete** as the PC starts to open the startup settings
      (BIOS). Is Secure Boot under **Settings > Security > Secure Boot**? Look only, change
      nothing, then exit without saving. Path you saw: ______

**Notes:**

## 9. Anything else?

Anything confusing, slow, too wordy or missing:

---

## Bringing results back

1. Copy **`greyhaven-raw.json`** (from step 2) into the project folder on the Linux laptop
   (`~/Documents/claude/greyhaven/`). It's ignored by git, so it stays private.
2. Paste this filled-in form into the chat, or save it over this file.
3. Optional: screenshots of anything that looked wrong.

The spare USB stick still has Ubuntu on it. Keep it for the next session, or erase it with
File Explorer (right-click the drive → Format) if you need it back.
