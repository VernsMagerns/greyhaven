# GreyHaven compatibility data

Improving ratings is a data change. You don't need to touch any C# code.

| File | What it is |
|---|---|
| `distros.yaml` | The distros, their install-ISO kernels, driver package sets and libfprint versions |
| `rules/*.yaml` | Curated rules: hand-checked knowledge about specific devices and device classes |
| `modules.yaml` | How to interpret particular kernel modules (e.g. bus drivers that don't really drive the device) |
| `caveats.yaml` | Things that can't be judged from Windows, with what to test from a live USB |
| `secure-boot-guides.yaml` | PC makers' official "turn Secure Boot off" instructions, matched by manufacturer/model |
| `dataset.yaml` | When the data was built (written by the index builder) |
| `indexes/` | Generated. Device ID lists extracted from official distro packages. Don't edit by hand; see `tools/GreyHaven.IndexBuilder/README.md` |

Curated files are CC-BY-4.0 (see [LICENSE](LICENSE)). How ratings and scores are worked
out is described in [docs/scoring.md](../docs/scoring.md).

## Writing a rule

Add a rule when GreyHaven gets a device wrong, or when there's a known issue the driver
lists can't show. Example:

```yaml
rules:
  - id: broadcom-wl-wifi                     # unique, lower-case
    title: Broadcom Wi-Fi that needs the "wl" driver
    match:
      ids: [pci:14e4:4365, pci:14e4:43b1]    # "bus:vendor:device", or add ":subvendor:subdevice"
      excludeIds: [pci:14e4:4365:14e4:4365]  # optional
    confidence: high                         # high | medium | low (default high)
    ratings:                                 # first matching "when" wins; last one has no "when"
      - when: { distros: [ubuntu], kernelAtLeast: "7.0" }
        rating: worksOutOfTheBox
        explanation: Plain English, no jargon. Placeholders - {distro}, {kernelSeries}, {driverTool}, {edition}, {driverPackages}.
      - rating: worksWithExtraDriver
        explanation: ...
    fix:                                     # required for worksWithExtraDriver
      driver: Broadcom STA ("wl")
      secureBoot: mokRequired                # none | mokMayBeRequired | mokRequired (DKMS = mokRequired)
      needsInternet: true
      steps:    { mint: [...], default: [...] }   # plain-English steps per distro id
      commands: { ubuntu: [sudo apt install broadcom-sta-dkms], mint: [...] }
    sources:                                 # required: link what you checked
      - title: Debian wiki - wl
        url: https://wiki.debian.org/wl
        distros: [ubuntu, mint]              # optional: only show this source for these distros
```

**Match fields** (all given fields must hold): `ids`, `excludeIds`, `vendor` (e.g.
`pci:10de`), `classCode` (PCI class prefix, e.g. `03`), `category` (e.g. `touchpad`), `bus`
(`pci`, `usb`, `hdAudio`, `hid`, `acpi`), `cpu: { architecture, vendors }`.

**Conditions** (`when`): `distros`, `kernelAtLeast`, `kernelBelow`, `preinstalledDriver`,
`driverPackage` (glob), `onlyDriverPackages` (glob), `secureBoot` (true/false).

Rules matching by ID or vendor override the kernel's driver lists. Rules matching only by
category, class or bus are "class rules" and rank below device-specific kernel matches.

**Rules for rules:**

- Use IDs, never device names.
- Every rule needs at least one source you've actually checked. Prefer official docs,
  kernel source or distro release notes.
- A source about one distro (release notes, a distro's own docs) should say so with
  `distros`, so each distro's details only cite what's relevant to it. Every distro needs at
  least one source per rule.
- If you're not sure, don't add the rule. "Unknown" is better than a wrong answer.
- Run `dotnet test`. Malformed rules fail with a message naming the file and rule.
