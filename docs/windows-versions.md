# Windows 10 and Windows 11

GreyHaven must "just work" on both. This page says what's tested automatically, what differs
between the two versions and how GreyHaven handles it, and how to test the version you don't
have, in a free virtual machine (VM) on a Linux PC.

## What CI tests (and what it can't)

GitHub has no Windows 10 or Windows 11 machines, only Windows **Server**:

| CI job | Runs on | What it proves |
|---|---|---|
| `test (windows-latest)` | Windows Server 2025 (same core as Windows 11 24H2) | All unit tests pass on Windows. |
| `windows-live-scan` | Windows Server 2025 | The real scanner (WMI, registry, Win32) runs end to end and its output can be rated and exported. |
| `ui-smoke` | Windows Server 2025 **and** Windows Server 2022 | The real app starts, the whole flow works (results, Show more, walkthrough, dark mode), and every control has a name for screen readers. Server 2022 lacks Windows 11's icon font, so Windows 10's icon fallback gets used. Screenshots are saved. |
| `test (ubuntu-latest)` | Linux | Everything that isn't Windows-specific, including the samples, which describe Windows 10 and 11 PCs. |

What CI **can't** show:

- Windows 10 itself (build 19045) or Windows 11 client editions (Home, Pro).
- Running as a normal user: CI runs as administrator.
- Real hardware: CI machines are virtual, with no real BitLocker, battery or webcam.
- How it looks and feels: contrast themes, Narrator, display scaling, dark mode following
  Windows' setting.

These are what the Windows session ([windows-results.md](windows-results.md)) covers. Record
which version each session used.

## Differences between Windows 10 and 11

| Area | Windows 10 | Windows 11 | How GreyHaven handles it |
|---|---|---|---|
| Version name | Registry says "Windows 10" | Registry *also* says "Windows 10" | Uses the build number (22000 or later = Windows 11). Tested. |
| CPU type check (`IsWow64Process2`) | Missing before version 1511 | Present | Falls back to .NET's answer; every scan read is guarded. |
| Secure Boot, Fast Startup, BitLocker readings | Same registry values and shell property | Same | No difference. Windows 11 turns on Device encryption more often, including "waiting for activation", which is mapped. |
| Free disk space | Same WMI class | Same | No difference. |
| Icon font in the app | "Segoe MDL2 Assets" only | "Segoe Fluent Icons" | Both GreyHaven and WPF's Fluent theme list both fonts; the symbols share codes. Checked on Server 2022 in CI and by V1 in the session. |
| Dark title bar | Windows 10 20H1+ uses one setting, 1809-1909 another | Same as 10 20H1+ | Tries the current setting, then the older one. |
| App text font | No "Segoe UI Variable" | Has it | GreyHaven ships its own fonts (Rubik, Lexend), so text looks the same on both. |
| Settings paths in the walkthrough | Update & Security → Recovery | System → Recovery | Chosen from the scanned Windows version. Tested. |
| Contrast themes | "High contrast" (Ease of Access) | "Contrast themes" (Accessibility) | Same system colours either way; GreyHaven follows them. |
| Smart App Control | Not present | Can block unsigned apps on fresh installs | Mentioned in the session form; code signing (milestone 8) is the real fix. |
| ARM PCs | Can't run x64 apps | Runs x64 apps by emulation | GreyHaven is x64; on Windows 10 on ARM it won't start (rare: that edition ended in 2025). |
| Windows 10 end-of-support note | Shown | Not shown | Hidden on LTSC editions, which are supported longer. |
| Installer (milestone 8) | | | To test: install, Start menu entry and uninstall on both. Added to Session 2. |

## Test the other version in a free virtual machine

Microsoft offers free 90-day evaluation copies for testing. They don't need a product key.

- **Windows 11**: Windows 11 Enterprise evaluation.
- **Windows 10**: Windows 10 Enterprise LTSC 2021 evaluation. It behaves like Windows 10
  22H2 for GreyHaven, except the end-of-support note is hidden (by design, see above).

The steps below use **virt-manager** on Ubuntu, which handles Windows 11's TPM and Secure Boot
needs. This laptop supports virtualization, with plenty of memory and disk. Allow about an
hour the first time, mostly waiting for Windows to install.

### 1. Install the VM tools (once)

Open a terminal and run:

```sh
sudo apt install virt-manager swtpm-tools ovmf
sudo usermod -aG libvirt $USER
```

Then log out and back in, so the second line takes effect.

### 2. Download Windows

- Windows 11: https://www.microsoft.com/en-us/evalcenter/download-windows-11-enterprise
- Windows 10: https://www.microsoft.com/en-us/evalcenter/evaluate-windows-10-enterprise
  (choose the LTSC 2021 ISO, 64-bit, English)

The page may ask for your name and email first. Save the ISO file in your Downloads folder.

### 3. Create the VM

1. Open **Virtual Machine Manager** from the app menu.
2. Click the **Create a new virtual machine** button (the screen with a star, top left).
3. Choose **Local install media (ISO image or CDROM)**, then **Forward**.
4. Click **Browse…**, then **Browse Local**, and pick the ISO you downloaded. It should
   detect "Microsoft Windows 11" or "Windows 10". **Forward**.
5. Memory **8192** MB, CPUs **4**. **Forward**.
6. Disk **80** GB. **Forward**.
7. Name it (for example "win11-test"), tick **Customize configuration before install**, then
   **Finish**.
8. In the window that opens, check **Overview → Firmware** shows **UEFI** (for Windows 11, pick
   the one ending in `secboot` so Secure Boot is on, like a real PC).
9. Windows 11 only: click **Add Hardware → TPM**, choose **Emulated**, version **2.0**, then
   **Finish**. (Newer virt-manager versions add this for you; skip if a TPM is already listed.)
10. Click **Begin Installation** (top left).

### 4. Install Windows

1. When the VM shows "Press any key to boot from CD", click inside the window and press a key
   quickly.
2. Follow the setup. Choose **Custom: Install Windows only**, then the empty drive.
3. At the account step:
   - Windows 11: choose **Sign-in options**, then **Domain join instead**, to make a local
     account.
   - Windows 10 LTSC: just type a user name.
4. When the desktop appears, you're ready. To release the mouse from the VM, press
   **Ctrl + Alt** (left side).

### 5. Run the session in the VM

In the VM, open Edge, sign in to GitHub, and follow [windows-results.md](windows-results.md)
from "Download the test build". In the form's first table, write the Windows version and
**Virtual machine**.

A VM can't test real hardware: its "parts" are virtual. Use it for the look, the walkthrough,
the themes, the keyboard and Narrator. Use your real PC for the scan checks (S1).

### Tips

- **Make a clean restore point:** right after installing, in Virtual Machine Manager choose
  the VM, then **View → Snapshots → +**. You can return to it any time.
- **When the 90 days run out**, delete the VM and repeat steps 3-5 with a fresh download.
- **Dark mode and contrast themes** work normally in the evaluation copies, because they
  count as activated.
