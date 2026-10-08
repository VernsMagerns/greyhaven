# Part E plan: GreyHaven for Linux (for review, nothing built yet)

Drafted 8 October 2026 (Pacific). The Windows app's goodbye screen will point here once it
exists (`GreyHavenInfo.LinuxCompanionUrl`, null until then).

## What it does

Someone has just installed Linux with GreyHaven's help. They open GreyHaven for Linux once and it:

1. **Checks everything works**, with the same kind of words as the Windows results screen
   ("Works", "One more step", "Not working yet"), and a fix for each problem.
2. **Suggests apps to set up first:** their own Windows apps' Linux versions (from the saved
   report, if they bring it) plus a short list everyone benefits from.
3. **Never runs anything by itself** (the rule agreed in Part C). Each "Get it" button opens the
   app's page in the software store, where the person clicks Install. Commands stay folded away
   for people who want them.

## 1. The checks

Read-only, no administrator password. Each line says what was checked and how to fix it.

| Check | How (read-only) | If it fails |
|---|---|---|
| Every part has a driver | `/sys/bus/pci/devices/*/driver`, `/sys/bus/usb/devices` | Names the part; links the same fix rules GreyHaven used on Windows |
| Graphics card uses the right driver | `/sys/class/drm`, `/proc/driver/nvidia/version` | NVIDIA: open "Additional Drivers" (Ubuntu/Mint) or check the NVIDIA ISO was used (Pop!_OS) |
| Secure Boot and the NVIDIA driver agree | `mokutil --sb-state`, kernel module loaded? | Explains the blue "Enrol MOK" screen, step by step |
| Sound works | PipeWire running (`pactl info`), output devices listed | Points to Settings > Sound; Scarlett: suggests alsa-scarlett-gui |
| Wi-Fi and Bluetooth | `rfkill list`, NetworkManager state | Airplane switch / missing firmware |
| Updates are done | the distro's updater state (apt lists age, `fwupdmgr get-updates` read-only) | Opens Update Manager / Software Updater |
| Flathub is on | `flatpak remotes` | Ubuntu: the one-time Flatpak setup from the guide |
| Windows still starts (Keep Windows installs) | Windows listed in `/boot/grub/grub.cfg` | Explains `os-prober` and the menu |
| The clock is right in both systems | `timedatectl` (local RTC vs UTC) | Explains the one setting, folded away |
| Disk space | `df` on `/` and `/home` | — |

On Linux the checks can be more definite than on Windows: the driver is either loaded or it isn't.

## 2. Suggested apps

Everything below was checked on the Flathub API on 8 October 2026 (verified = Flathub confirms
the listing comes from the developer).

**Everyone**

| App | What for | Get it | Notes |
|---|---|---|---|
| Bazaar | A fast app store for Flathub apps | Flathub `io.github.kolunmi.Bazaar` (verified) | Optional: Mint and Pop!_OS already have a store. Suggest it mainly on Ubuntu, where the built-in App Center prefers Snaps |
| Flatseal | See and change what each Flatpak app can access | Flathub `com.github.tchx84.Flatseal` (verified) | |
| PeaZip | Opens .zip, .rar, .7z (like 7-Zip/WinRAR) | Flathub `io.github.peazip.PeaZip` (verified) | |
| Mission Center | Looks like Windows' Task Manager | Flathub `io.missioncenter.MissionCenter` (verified) | |
| Pika Backup | Simple backups | Flathub `org.gnome.World.PikaBackup` (verified) | |

**If they game**

| App | Get it | Notes |
|---|---|---|
| Steam | Flathub `com.valvesoftware.Steam`, or Ubuntu's `steam-installer` | Not marked verified on Flathub (Valve doesn't publish it there itself); GreyHaven already lists both routes |
| Heroic (Epic, GOG, Amazon) | Flathub `com.heroicgameslauncher.hgl` (verified) | |
| ProtonUp-Qt or ProtonPlus | Flathub `net.davidotek.pupgui2` / `com.vysp3r.ProtonPlus` (both verified) | For games that need a newer Proton. Suggest one, not both |
| Discord | Flathub `com.discordapp.Discord` (verified, Discord Inc.) | |

**If they have the hardware or apps for it** (driven by the scan and the saved report)

| App | When | Get it |
|---|---|---|
| Easy Effects | A microphone or headset (equaliser, noise reduction, like NVIDIA Broadcast's audio part) | Flathub `com.github.wwmm.easyeffects` (verified) |
| LACT | AMD graphics card (fan curves, power, like AMD Adrenalin's tuning tab) | Flathub `io.github.ilya_zlobintsev.LACT` (verified) |
| OBS Studio | They had OBS on Windows | Flathub `com.obsproject.Studio` (verified) |
| DaVinci Resolve | They had it on Windows | Blackmagic's website only (no store) |
| Extension Manager / Refine | Ubuntu (GNOME) and they want it to look more like Windows | Flathub (both verified) |

**A file manager:** Ubuntu, Mint and Pop!_OS each come with one (Files, Nemo, COSMIC Files) that
does what File Explorer does. I'd suggest *not* recommending a replacement. Dolphin
(`org.kde.dolphin`, verified) as a Flatpak can't see the whole system properly from inside its
sandbox. If you had a specific file manager in mind, tell me which one and why.

**DaVinci Resolve needs a clear warning.** The free Linux version can't open H.264/H.265 video
(most phone and camera videos) or AAC audio. That's because of codec licensing; the paid Studio
version can. People coming from Windows won't expect this. GreyHaven should say so and suggest
Kdenlive for everyday editing.

## 3. How it's built (decisions for you)

1. **UI toolkit. Recommended: Avalonia** (MIT). WPF is Windows-only, but Avalonia uses the same
   XAML style and MVVM. Our view models (GreyHaven.Presentation) and the Core, Compat, Ratings
   and Reports projects carry over unchanged. Only the views and a `Scan.Linux` project are new.
   The other option is GTK4 through gir.core: it would look more native on GNOME, but every
   screen would have to be rewritten.
2. **Packaging. Recommended: Flathub plus an AppImage.**
   - A Flathub app runs in a sandbox. The checks need read-only access to `/sys`, `/proc`,
     `/boot` and `flatpak-spawn --host` for `mokutil`/`pactl`. Flathub reviews ask for a reason
     for that, and a read-only hardware checker is a reasonable one. If they refuse, we fall
     back to the AppImage plus a .deb.
   - People find it where they find every other app: in the store.
3. **"Get it" buttons** open `appstream://<flathub id>`. That URL opens the right store on each
   distro: GNOME Software, Mint's Software Manager, the COSMIC Store, or Bazaar if installed.
   A folded-away "Or type this" block shows the `flatpak install` command, as in the Windows app.
4. **Bringing the Windows report:** the saved report file (Documents\GreyHaven on the Windows
   drive) is readable from Linux. GreyHaven offers to open it to rebuild "Your apps", and only
   if the person agrees. Nothing is uploaded.

## 4. Order of work (each step committed, as before)

1. A `Scan.Linux` raw-data reader plus samples from this laptop and a Linux VM, with tests on CI
   (Linux, so no Windows session needed).
2. The checks, as rules in `data/` (same style as `rules/`), with sources.
3. The suggestions list as `data/apps/linux-starters.yaml` (the tables above), with a CI check
   against the Flathub API.
4. The preview (browser), for your review. **Pause here.**
5. The Avalonia app, then packaging.
6. Set `LinuxCompanionUrl`, so the Windows goodbye screen links to it.

## Questions for you

1. Avalonia and Flathub (plus AppImage): OK?
2. The file manager: leave the built-in one, or did you have one in mind?
3. Bazaar: only on Ubuntu (my suggestion), or everywhere?
4. Anything else to check after installing, or other apps you'd always want on the list?

## Sources

- Flathub API, `https://flathub.org/api/v2/appstream/<id>`, checked 8 October 2026 for every ID above
- Bazaar: https://github.com/kolunmi/bazaar
- DaVinci Resolve codecs on Linux: https://www.provideocoalition.com/aac-audio-kdenlive-beats-davinci-resolve-studio-on-linux/
  and https://www.tal.org/tutorials/blackmagic-davinci-resolve-linux
