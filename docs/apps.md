# Will my apps work? (Part C)

*Design notes, 7 October 2026. Code: `src/GreyHaven.Core/Apps`, `src/GreyHaven.Compat/Data/AppCatalog.cs`,
`src/GreyHaven.Ratings/AppRater.cs`, `src/GreyHaven.Scan.Windows/WindowsApps.cs`. Data: `data/apps`.*

## Finding apps (no administrator rights)

| Where | What's kept |
|---|---|
| Windows' uninstall lists: `HKLM` (64- and 32-bit) and `HKCU` `...\CurrentVersion\Uninstall` | Name, publisher; the Steam app ID if the entry is a Steam game. Hidden entries (`SystemComponent`), updates (`ReleaseType`, `ParentKeyName`) are dropped. |
| Microsoft Store apps for this user (`HKCU\...\AppModel\Repository\Packages`) | Package name and display name. Resource packages, libraries and Microsoft's built-in apps are dropped (except Xbox, Minecraft). |
| Steam: `libraryfolders.vdf` and each library's `appmanifest_*.acf` | App ID and name. Proton, runtimes and redistributables are dropped. |
| Epic Games: `%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item` | Display name and catalog namespace. |

Never kept: install folders, file paths, versions, uninstall commands. The Windows user name is
replaced with `<user>` (Roblox names its entry "Roblox Player for <name>"). Filters for runtimes,
drivers and updates are in Core (`AppInventory`) and tested on Linux.

## Ratings

Works (Linux version) · Works with a helper (Proton, Wine, Lutris, Heroic; with confidence) ·
Use the website · Won't work (reason, plus alternatives) · Not needed · Not sure. First source wins:

1. **GreyHaven's own list** (`windows-apps.yaml`, 62 apps; `linux-apps.yaml`, 54 Linux apps and
   alternatives). Every claim has a source, checked 2026-10-07. Where to get each Linux app comes
   from the Flathub API (and whether the developer verified the listing), the Snap Store API (only
   verified or starred publishers) and Launchpad (apt packages in Ubuntu 24.04 and 26.04).
2. **Are We Anti-Cheat Yet** (MIT): 1,167 games with anti-cheat, matched by Steam ID, Epic
   namespace or exact name. Supported → works with Proton; Running → may not work online;
   Denied/Broken/Planned → won't work.
3. **ProtonDB** (Open Database License): a summary of the official report dump of
   1 October 2026, 20,018 Steam games. Reports from the last two years; at least 5 reports.
   80%+ "works" → works with Proton (high confidence with 20+ reports); 50–79% → low confidence;
   below 50% → won't work.
4. Otherwise **Not sure**. Never a guess.

Apps that depend on hardware (OBS, Canon EOS Utility → OBS with a capture card, Focusrite
Control → its replacement) show that part's rating, e.g. "Camera (HD Pro Webcam C920): Works
right away".

### Data sources: what was checked, and what wasn't used

- **Are We Anti-Cheat Yet:** MIT licence; imported with `tools/apps/import.py anticheat`.
- **ProtonDB:** the website's robots.txt blocks AI crawlers (including Anthropic's), so
  protondb.com is never fetched. Its official report dumps on GitHub
  (github.com/bdefore/protondb-data) are released under the ODbL, so GreyHaven summarises a dump
  with `tools/apps/import.py protondb <dump>`. The summary is a derived database and stays ODbL
  (`data/apps/LICENSE-protondb.md`), separate from GreyHaven's CC-BY-4.0 data.
- **Not used:** AlternativeTo (terms forbid scraping), Steam's store API (unofficial, terms
  unclear), WineHQ AppDB (most entries are years old).
- **Your examples:** VALORANT (Vanguard anti-cheat, "Denied"); FluidVox (Windows and Mac only)
  → **OpenWhispr** (spelled this way; MIT licence, Linux .deb and AppImage); Canon EOS Utility
  (Windows and Mac only) → Entangle for tethered shooting, or OBS with an HDMI capture card.

## Privacy

The app list never leaves the PC: matching uses the built-in data only. Saved reports leave it
out unless the person ticks "Include my apps" (`--include-apps` in the CLI). The public phone
guide never has it, only a line saying it's in the printed or saved copy. `--dump-raw` includes
app names (the CLI help says so). If online lookups for apps are ever added: off by default,
app names only, PRIVACY.md updated first.

## Must-have apps

People can mark apps "I need this". If one won't work (or might not), the results headline
says so, and the install choice suggests keeping Windows ("You can still use VALORANT on
Windows") and warns on Replace Windows.

## Installing apps after switching

The printed and saved guide (never the public phone guide) has an "Install your apps" checklist:
one line per Linux app, with where to get it on the chosen distro:

| | Ubuntu | Linux Mint | Pop!_OS |
|---|---|---|---|
| App store | App Center (Snap) | Software Manager (Flathub, verified apps only, + apt) | COSMIC Store (Flathub + apt) |
| Order tried | Snap → apt → website → Flathub (needs setup) | verified Flathub → apt → website | Flathub → apt → website |

### One-step install: tradeoffs

The checklist ends with an optional "Or install them all at once" block: one `sudo apt install`,
one `flatpak install -y flathub …` and one `sudo snap install …` line (classic snaps on their
own lines), and on Ubuntu the two Flatpak setup lines from flathub.org first.

- **For:** one paste, readable lines, official stores only, nothing downloaded from websites.
- **Against:** it asks people to paste into a terminal, which beginners are rightly wary of; it
  needs the password; a typo-free paste is required (a QR or copy button helps); apps from
  websites (e.g. Chrome on Mint, OpenWhispr) aren't covered.
- **Recommendation:** keep the app-store checklist as the main way, and the commands folded away
  for people who want them. Don't let GreyHaven run anything on Linux itself.
