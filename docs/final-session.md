# The last steps to launch

Three parts, in order. Tick boxes by changing `[ ]` to `[x]`, and write anything odd under
**Notes**. Bring the filled-in file back (or paste it into the chat) when you're done.

| Part | What | Time |
|---|---|---|
| A | Set up two Windows test computers (virtual machines) on this laptop | About 1 hour, mostly waiting |
| B | The test session in them | About 1½ hours |
| C | Go public | About 15 minutes, plus my part |

Your contact address for conduct reports can be sent any time before Part C.

---

## Part A: Set up the virtual machines (once)

A virtual machine (VM) is a pretend computer in a window. Anything done inside it, like
encrypting its drive, can't affect this laptop. Full details are in
[windows-versions.md](windows-versions.md) if you need them.

### A1. Install the VM tools (5 minutes)

In a terminal on this laptop:

```sh
sudo apt install virt-manager swtpm-tools ovmf
sudo usermod -aG libvirt $USER
```

Then **log out and back in**.

- [ ] Done. "Virtual Machine Manager" is in the app menu.

### A2. Download Windows (free 90-day trial copies, no key needed)

- **Windows 11 Enterprise:** https://www.microsoft.com/en-us/evalcenter/download-windows-11-enterprise
  (it has BitLocker, which Part B needs)
- **Windows 10 Enterprise LTSC 2021:** https://www.microsoft.com/en-us/evalcenter/evaluate-windows-10-enterprise
  (64-bit, English)

They're about 5 GB each. Save them in Downloads.

- [ ] Both downloaded.

### A3. Create the Windows 11 VM

1. Open **Virtual Machine Manager** → **Create a new virtual machine** (top-left button).
2. **Local install media** → **Forward** → **Browse… → Browse Local** → pick the Windows 11 file
   → **Forward**.
3. Memory **8192**, CPUs **4** → **Forward**. Disk **80** GB → **Forward**.
4. Name it `win11-test`, tick **Customize configuration before install**, then **Finish**.
5. **Overview → Firmware:** pick the one ending in **secboot**.
6. If no **TPM** is listed on the left: **Add Hardware → TPM → Emulated, version 2.0 → Finish**.
7. **Begin Installation**. When it says "Press any key to boot from CD", click inside and press a key.
8. Windows setup: **Custom: Install Windows only** → the empty drive. At the account step,
   **Sign-in options → Domain join instead**, and make a local account (for example `admin`)
   **with a password**. You'll need that password in Part B.
9. When the desktop appears: in Virtual Machine Manager, **View → Snapshots → +**, and name it
   `fresh`. It's a restore point you can go back to.

(**Ctrl + Alt** on the left releases the mouse from the VM.)

- [ ] Windows 11 VM ready, with a `fresh` snapshot.

### A4. Create the Windows 10 VM

The same as A3 with the Windows 10 file. Name it `win10-test`, and also add a **TPM** (step 6).
At the account step, just type a user name and a password.

- [ ] Windows 10 VM ready, with a `fresh` snapshot.

**Notes:**

---

## Part B: The test session

In each VM, open Edge and sign in to GitHub (the repository is still private). Use this download:
https://github.com/VernsMagerns/greyhaven/releases/download/latest-test/GreyHaven-Setup.exe

### B1. Windows 11: install, use, uninstall (15 minutes)

- [ ] Double-click **GreyHaven-Setup.exe**. If Windows shows "Windows protected your PC", click
      **More info → Run anyway**. Write down exactly what it showed: ______
- [ ] The installer shows the lighthouse, a licence link and **Install**. Windows asks permission
      once. It finishes with a **Launch** button that opens GreyHaven.
- [ ] **Start menu** has GreyHaven with the lighthouse icon. So does the taskbar while it runs.
- [ ] **Scan my PC** works. (The parts are virtual, so the ratings themselves don't matter.)
- [ ] **Check a game**: type a game you know. Results appear as you type.
- [ ] **Show me how to switch** → go through to the **Restart** screen. **Don't restart.**
- [ ] The speaker and moon buttons (top right) work.
- [ ] **Uninstall**: Settings → Apps → Installed apps → GreyHaven → **Uninstall**. It's gone from
      the Start menu, and `C:\Program Files\GreyHaven` is gone.

**Notes:**

### B2. Windows 11 with BitLocker (required, 30 minutes)

Many Windows 11 laptops come encrypted, so this matters.

1. Right-click Start → **Terminal (Admin)** → **Yes**. Type:
   ```
   manage-bde -on C: -UsedSpaceOnly -SkipHardwareTest
   ```
   Restart the VM when it asks. Then, in Terminal (Admin), run `manage-bde -status C:` until it
   says **Fully Encrypted** (a few minutes).
2. Install GreyHaven again (B1) and **Scan my PC**.

- [ ] Results/walkthrough say the drive **is encrypted**.
- [ ] **Keep Windows too** is blocked until encryption is off, and says why.
- [ ] **Get Windows ready** offers to **turn off encryption**. It explains first, then Windows asks
      permission, then it says it's done or working on it.
- [ ] When `manage-bde -status C:` says **Fully Decrypted**, close GreyHaven, open it and scan
      again: **Keep Windows too** is allowed now.
- [ ] **Replace Windows** says clearly what happens to an encrypted drive.

**Notes:**

### B3. A standard (non-admin) account (20 minutes)

This tests the fix from 10 October: the helper had to be allowed to connect back when it runs as
a different user.

1. In **Terminal (Admin)**, turn Fast Startup on, so GreyHaven has something to change:
   ```
   powercfg /hibernate on
   reg add "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power" /v HiberbootEnabled /t REG_DWORD /d 1 /f
   ```
2. Make a standard account and switch to it:
   ```
   net user tester GreyHaven1! /add
   ```
   Then Start → your name → **Sign out**, and sign in as `tester` (password `GreyHaven1!`).
3. Open GreyHaven (it's installed for everyone) → **Scan my PC** → **Show me how to switch** →
   **Get Windows ready**.

- [ ] **Turn off Fast Startup** → **Yes, do it**. Windows asks for an **administrator's**
      password (not just Yes/No). Type the admin password from A3.
- [ ] It says **✓ Done**. (Before the fix, it would have said the helper didn't answer.)
- [ ] **Undo** works the same way.

**Notes:**

### B4. Windows 10 (20 minutes)

In the `win10-test` VM:

- [ ] Install with **GreyHaven-Setup.exe**: same as B1.
- [ ] Icons show properly (no empty squares), in light and dark.
- [ ] Scan, results and walkthrough to the Restart screen look right.
- [ ] Uninstall works.

**Notes:**

### B5. Upgrade (10 minutes)

When I've made a test release (`v0.9.0`, a draft only you can see), install it **over** the
version from B4 in the Windows 10 VM. Get it from the draft release's page:
https://github.com/VernsMagerns/greyhaven/releases

- [ ] It installs without complaining, and there's still just **one** GreyHaven in Settings → Apps.

**Notes:**

---

## Part C: Go public

Only once Part B is done and anything it found is fixed.

### C1. Before (you)

- [ ] Send me a **contact address for conduct reports**. It becomes public, so a separate address
      (for example a new free email account just for GreyHaven) is better than your own.
- [ ] Tell me **"ready to go public"**.

### C2. My part

I'll switch on public links (`IsRepositoryPublic`), put your contact address in place, run the
release check, set the version to 1.0.0, delete the `v0.9.0` test draft, and make a **draft** `v1.0.0`
release for you to read.

### C3. Make it public (you, 10 minutes)

1. **Make the repository public:** https://github.com/VernsMagerns/greyhaven/settings → bottom of
   the page, **Danger Zone** → **Change repository visibility** → **Public**. Confirm.
2. **Turn on the download page:** https://github.com/VernsMagerns/greyhaven/settings/pages →
   Source: **GitHub Actions**.
3. **Social preview:** https://github.com/VernsMagerns/greyhaven/settings → **Social preview** →
   **Edit** → upload `assets/brand/social-preview.png` from the project folder.
4. **Private vulnerability reporting:** https://github.com/VernsMagerns/greyhaven/settings/security_analysis
   → **Enable**.
5. **Branch rule:** https://github.com/VernsMagerns/greyhaven/settings/rules (or **Branches**):
   check the rule for `main` shows as **active**.
6. **Publish the release:** https://github.com/VernsMagerns/greyhaven/releases → the **v1.0.0**
   draft → read the notes → **Publish release**.

- [ ] All six done.

### C4. My checks afterwards

Signed out, I'll check that the download button works, the download page is live, and the update
check sees v1.0.0. Then: apply to SignPath for code signing ([going-public.md](going-public.md),
steps 11 to 14).

**Notes:**
