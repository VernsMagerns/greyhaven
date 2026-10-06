# How GreyHaven rates hardware

GreyHaven rates every component of a PC separately for each distro (Linux Mint, Ubuntu,
Pop!_OS), then combines those ratings into one score per distro and recommends the best one.
This page explains exactly how. The code is in `src/GreyHaven.Ratings/`.

## Ratings

| Rating | Meaning |
|---|---|
| **Works out of the box** | Works on the install USB and after installing, with nothing extra to do. |
| **Works with extra driver/config** | Works after one documented step: installing a driver package, using the distro's NVIDIA ISO, or installing updates. GreyHaven shows the exact steps and commands. |
| **Partial/known issues** | Basic function works, but some features don't, or it depends on the exact model. |
| **Unsupported** | Known not to work. |
| **Unknown** | GreyHaven has no reliable data. It never guesses. |

Every rating has a plain-English explanation, the source it's based on (with a link where
one exists), and a **confidence**: high, medium or low. Ratings that are Unknown or low
confidence are labelled as based on limited data.

## Where ratings come from

For each component and distro, GreyHaven uses the **first** source below that has something to say:

1. **Curated device and vendor rules** (`data/rules/`). These are hand-written and checked
   against official sources, and they override raw data. For example, `bcma` "claims" some
   Broadcom Wi-Fi chips but doesn't actually drive them; the Broadcom rule knows that.
   Usually high confidence.
2. **libfprint lists** (fingerprint readers only). Each distro's libfprint package lists
   the readers it supports and the ones known not to work.
3. **A kernel driver that lists this exact device ID.** This comes from the kernel on the
   distro's official install ISO (see `data/distros.yaml`). Medium confidence: having a
   driver is strong evidence but doesn't guarantee every feature works.
4. **A driver package that lists this exact device ID**, from the distro's package index
   ("Modaliases"; the same data `ubuntu-drivers` uses). Rated *works with extra driver*.
5. **Curated class rules**, for example "Windows Precision Touchpads use a standard protocol
   Linux supports".
6. **A kernel driver for the whole device class**, for example NVMe SSDs, USB 3 controllers
   or USB webcams. Confidence comes from `data/modules.yaml` (high for long-standard
   classes like NVMe and AHCI).
7. **linux-hardware.org community probes** (online, optional). This needs at least 5
   probes that report the device as working or failing. If at least 80% report it working,
   the rating is *works out of the box*; otherwise it is *partial*. Always **low**
   confidence. Fewer probes leave the rating Unknown, but the page is still linked.
8. Otherwise: **Unknown**.

**Install ISO vs. updates.** Ratings use the kernel on the install ISO, because that's what
runs on the live USB and in the installer. If that kernel lacks support but the kernel
installed with system updates has it, the rating becomes *works with extra driver/config*,
and the fix is to install updates.

**Matching is conservative.** Windows doesn't reveal every field Linux matches on (for
example a HID device's group). Unknown fields only match a wildcard, so GreyHaven never
assumes a driver binds when it can't tell.

**Approximate data.** Some indexes are approximations: Pop!_OS doesn't publish the exact
kernel packages from its ISOs, so those indexes are built from the same upstream release.
Kernels before about 7.0 don't record built-in drivers' devices, so their class-wide
patterns are borrowed from a newer kernel. A rating that rests on approximate data is
**capped at medium confidence** and carries a note saying why.

**ARM PCs.** The indexes describe x64 kernels, so on an ARM PC everything except the
processor is rated Unknown.

## Importance

| Importance | Components | Weight |
|---|---|---|
| Critical | Processor, graphics, storage controller, NVMe SSD, Wi-Fi on laptops (or when there's no wired network), wired network on desktops (or when there's no Wi-Fi) | 3 |
| Important | Sound, Bluetooth, touchpad, camera, USB controllers, the other network adapter | 1.5 |
| Minor | Everything else: HDMI audio, fingerprint reader, card reader, Thunderbolt, touchscreen | 1 |

Chipset parts (bridges, SMBus, management engine), external input devices and unclassified
devices are rated and shown but **not scored**. Otherwise dozens of trivially supported
parts would dilute the ones that matter.

## The summary score (0–100)

Points per rating: *works out of the box* 100, *extra driver/config* 80, *partial* 40,
*unsupported* 0.

```
score = sum(weight x points) / sum(weight)      over scored components that aren't Unknown
```

Unknown components are left out of the average, so missing data doesn't count as
"broken". They lower **coverage** instead: the weighted share of components that could be
rated, which is shown next to the score.

Then caps apply. The lowest applicable cap wins, and its reason is always shown:

| Condition | Score capped at | Why |
|---|---|---|
| Any **critical** component is *unsupported* | **40** | One essential part not working should clearly drag the score down. |
| Any **critical** component is *unknown* | **70**, marked *incomplete* | Can't call a PC "great" without knowing its essentials. |
| Any **critical** component is *partial* | **79** | A partly working essential part rules out "great". |

Bands: **Great** 90–100, **Good** 70–89, **Fair** 50–69, **Poor** below 50.

### Worked example

A desktop where the graphics card needs a driver (critical, 80), sound is partial
(important, 40) and the card reader works (minor, 100):

```
(3 x 80 + 1.5 x 40 + 1 x 100) / (3 + 1.5 + 1) = 400 / 5.5 = 72.7  ->  73 (Good)
```

## The recommendation

Distros are ordered by:

1. highest score
2. fewest *extra driver* steps
3. fewest system changes (for example, having to turn off Secure Boot for Pop!_OS)
4. `tieBreakRank` in `data/distros.yaml` (Mint, then Ubuntu, then Pop!_OS)

The paragraph names the components the winner handles better than the runner-up, says who
the distro suits, counts the extra steps and unrated parts, and mentions ARM limitations.
If no distro supports the processor at all, GreyHaven doesn't recommend switching.

### Two paths when Secure Boot is involved

If the best distro needs Secure Boot turned off, and Secure Boot is on (or GreyHaven
couldn't read it), the user chooses between two paths:

- **Easy (default):** `secureBootEasyModeDistro` in `data/distros.yaml` (Ubuntu), which
  keeps Secure Boot on. The paragraph explains every extra step it needs in plain words,
  for example installing the NVIDIA driver and confirming the one-time "Enroll MOK" screen.
- **Advanced:** the best-supported distro (e.g. Pop!_OS). It comes with a short notice:
  Secure Boot must be turned off, what that means, and that it can be turned back on later
  (though Pop!_OS won't start while it's on). It also links a how-to guide: the PC maker's
  official instructions when the maker is recognised (`data/secure-boot-guides.yaml`),
  otherwise [docs/secure-boot.md](secure-boot.md).

GreyHaven never changes Secure Boot itself; it is instructions-only. The chosen path
decides which distro's fixes are shown, adds "turn off Secure Boot" to the pre-switch
checklist (Advanced only), picks the live USB creator's default ISO (e.g. Pop!_OS's NVIDIA
ISO), and is recorded in exported reports.

## Data freshness

Every report shows when the bundled data was built (`data/dataset.yaml`). GreyHaven tells the
user to check for an update when the data is more than **90 days** old, or when (with online
checks allowed) a newer release of a supported distro is found: a new Ubuntu point release,
a new Linux Mint version, or a new Pop!_OS ISO build or version. A weekly GitHub Action
(`.github/workflows/distro-releases.yml`) runs the same check and opens an issue for each
new release.

## Things that can't be judged from Windows

These are always listed, with what to test from a live USB (`data/caveats.yaml`): sleep and
wake, battery life (laptops), switching between two graphics chips, and fingerprint
enrolment.
