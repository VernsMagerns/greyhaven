# The white-glove walkthrough (Part B)

*Design notes, 7 October 2026. Content: `src/GreyHaven.Reports/Journey.cs` (JourneyBuilder) and
`PhoneGuideBuilder.cs`. Preview: `tools/ui-preview/walkthrough.js`. The WPF app and the saved
report keep the current 9-step walkthrough until Part D swaps them over.*

## The flow

| Part | Screens (in GreyHaven on Windows) | What GreyHaven does |
|---|---|---|
| 1. Get ready | How do you want to install? · Back up your files · Get Windows ready · *Change a startup setting* (only when needed) | Checks free space and encryption the moment a choice is picked. Backup is required (every box ticked) only when replacing Windows. Safe Windows changes are one click, with a confirm step and an undo. |
| 2. Make your USB stick | One screen | Finds the stick, refuses ones that are too small, downloads, checks, writes, reads back. |
| 3. Take your guide | One screen | QR code to the phone guide (once public), print, open/save, plus a copy on the Windows drive. |
| 4. Restart | One screen | Final checklist, then one button: restart to Windows' Advanced startup, pick the stick. |
| 5. On the USB stick | One "after you restart" look-ahead in the app | The phone/printed guide covers Try Linux, Install, After installing. |

Changes from the brief, and why:

- **"Keep Windows too" is pre-selected and marked "Suggested"** when it's possible (enough
  space; not Pop!_OS, whose installer can't do it by itself). A first-timer can then just press
  Next, and nothing is lost if they change their mind after trying Linux. When it isn't
  possible, nothing is pre-selected and the card says why.
- **Startup settings get their own screen, only when needed** (Secure Boot on the Advanced path,
  or Intel RST/VMD storage). Firmware updates are a folded "Nice to have" at the bottom of Get
  Windows ready, never a task.
- **Encryption, keeping Windows:** turning BitLocker off is a one-click task
  (`manage-bde -off C:`, needs the elevated helper), with "Or do it yourself" pictures. It's
  undoable from Control Panel but takes an hour or more, so the confirm text says so.
  **Replacing Windows:** only "Save your recovery key" (the drive is erased anyway; the key is
  insurance until then).
- **Pulling milestone 6 forward also pulls milestone 5 forward.** Writing to a USB stick, turning
  off Fast Startup (HKLM) and turning off BitLocker all need administrator rights, so the
  elevated helper (one action per elevation, UAC prompt each time) has to come first in Part D.

## Findings

### Restarting into the USB stick

- `shutdown /r /o /t 0` restarts to Windows' **Advanced startup** ("Choose an option") screen.
  It needs only the "Shut down the system" user right, which standard users have on Windows 10
  and 11, so **no administrator prompt** ([Microsoft: shutdown](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/shutdown)).
- There, **Use a device** lists the firmware's boot entries (often "UEFI: <stick name>"). It's
  only shown on UEFI PCs, and some firmware doesn't list USB sticks there. So the restart screen
  has a fallback: the maker's boot-menu key (MSI: F11) under "USB stick not in the list?".
- Not used: setting UEFI `BootNext` with `bcdedit /set {fwbootmgr} bootsequence`. It needs
  administrator rights and the stick to already have a firmware boot entry, which many PCs only
  create while the menu is open. Could be an option later, behind the elevated helper.
- To check on Windows (Part D session): the button on a standard account; whether "Use a device"
  lists the stick on the maintainer's MSI board.

### Checking the download

| Distro | Fingerprint (checksum) | Signature | Source |
|---|---|---|---|
| Ubuntu | `SHA256SUMS` | `SHA256SUMS.gpg`, Ubuntu CD Image key `8439 38DF 228D 22F7 B374 2BC0 D94A A3F0 EFE2 1092` | [Ubuntu tutorial](https://ubuntu.com/tutorials/how-to-verify-ubuntu) |
| Linux Mint | `sha256sum.txt` | `sha256sum.txt.gpg`, Linux Mint key `27DE B156 44C6 B3CF 3BD7 D291 300F 846B A25B AE09` | [Mint guide](https://linuxmint-installation-guide.readthedocs.io/en/latest/verify.html) |
| Pop!_OS | `sha_sum` from `https://api.pop-os.org/builds/24.04/<intel\|nvidia>` (HTTPS) | none published | [System76: live disk](https://support.system76.com/articles/live-disk/) |

GreyHaven will ship the two signing keys' fingerprints in `data/distros.yaml`, verify the
signature with an OpenPGP library (BouncyCastle, MIT), then the file's SHA-256. Pop!_OS relies on
HTTPS to System76's API, and the screen says it's a checksum only.

### Writing the stick safely

Raw writes (`\\.\PhysicalDriveN`) need administrator rights. Safety rules for the helper:
only disks whose bus type is USB and which Windows marks removable; never the system or boot
disk, or any disk with a Windows page file; between 8 GB and 256 GB; the person ticks "I understand
everything on this stick will be erased" naming the stick and size; volumes are locked and
dismounted before writing; Cancel works at every stage; the stick is read back and compared.

### The guide on the USB stick: recommended against (for now)

An installer stick written byte-for-byte is a read-only ISO image. Adding a guide means adding a
partition after the image and editing its partition tables, as persistence tools do
([casper](https://manpages.ubuntu.com/manpages/noble/man7/casper.7.html)). That risks the
stick not starting on some PCs, and the read-back check would no longer cover the whole stick.
Instead:

- **On the Windows drive:** `Documents\GreyHaven\GreyHaven guide.html`. The live session can open
  it from Files unless the drive is encrypted or in RST/VMD mode (then it isn't offered).
- **Phone:** a QR code to the static guide on GitHub Pages, once the repository is public. Only
  the person's *choices* go in the address, after `#`, which browsers never send to the server
  (`v=1&d=ubuntu&e=desktop&i=keep&w=11&f=desktop&k=F11&x=gpu`): no part IDs, names or serials.
- **While the repository is private:** no QR code (Pages for private repositories needs a paid
  plan). Print, open/save the guide, or the Windows-drive copy. A local Wi-Fi server from the
  app was considered and rejected: listening on the network triggers a Windows Firewall prompt
  that needs administrator rights, and the page dies when the PC restarts.

### Pictures that enlarge

- **Saved report and phone guide (no scripts):** each picture is a link to itself; CSS `:target`
  shows it full-screen over a dark backdrop. Close with the big "Close picture" link, a tap
  outside, or Back. Links are announced as "Enlarge picture: <alt text>". Hidden under forced
  colours, like the pictures.
- **App (Part D):** a picture button opens a window-sized overlay; Escape or Close returns focus
  to the picture.

## Still to check

- MSI's Secure Boot path (Settings > Security > Secure Boot) was confirmed through search only;
  MSI's own FAQ blocks automated checks. Check on the maintainer's MSI board.
- The installer's desktop icon name in the Ubuntu 26.04 live session ("Install …").
