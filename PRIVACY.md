# GreyHaven privacy policy

*Draft, 7 October 2026. Applies to GreyHaven 0.1 and later.*

GreyHaven checks your PC's parts on your own PC. It has **no accounts, no tracking, no
analytics and no ads**, and it never sends anything about you to the GreyHaven project.

## What GreyHaven reads on your PC

To check your PC, GreyHaven reads:

- **Part ID numbers**, like `pci:8086-2723` (the maker and model codes every part reports), and
  the parts' names as Windows shows them.
- **Windows settings that matter for Linux:** Windows version, Secure Boot, BitLocker or Device
  encryption on or off, Fast Startup, how the storage is set up, the processor type, and free
  space on the Windows drive.
- **Who made the PC and its motherboard**, like "Dell" or "MSI", to pick the right instructions.

It **never** reads your files, documents or photos, your name or user name, your PC's name,
passwords, serial numbers, network (MAC) addresses, your location, or the names of Bluetooth
devices you've paired.

## What GreyHaven sends, and when

GreyHaven only goes online for the things below. Nothing is sent while you just scan with the
online check turned off.

| When | Sent to | What's sent |
|---|---|---|
| **Online check** (the "Also check online for parts we don't know" box on the first screen, on by default; you can untick it) | linux-hardware.org | The ID number of each part GreyHaven doesn't recognise, one at a time, like `pci:8086-2723`. Nothing else about your PC. |
| Online check | Linux Mint's download mirror (mirrors.edge.kernel.org), Ubuntu (changelogs.ubuntu.com) and System76 (api.pop-os.org) | Nothing about you or your PC: GreyHaven just reads their lists of new versions, so it can tell you if its information is out of date. |

As with any website, the services above see your internet (IP) address and that the request
comes from "GreyHaven" and its version. GreyHaven doesn't control what they do with it; see
their own privacy policies.

### The phone guide (the QR code)

Before your PC restarts into Linux, GreyHaven can show a QR code. Scanning it opens GreyHaven's
phone guide at **greyhaven-guide.pages.dev** in your phone's browser. GreyHaven itself sends
nothing: the code is only drawn on your screen.

- **The page is the same for everyone** who picked the same version of Linux and install choice
  (for example, Ubuntu, keeping Windows). It's hosted by Cloudflare Pages. When your phone loads
  it, Cloudflare sees your phone's internet (IP) address and browser, as with any website; see
  [Cloudflare's privacy policy](https://www.cloudflare.com/privacypolicy/). GreyHaven doesn't use
  analytics or cookies on it.
- **The personal touches stay on your phone.** They're in the part of the address after "#",
  which browsers never send to the website. They're limited to: your PC's maker (like "MSI"), its
  startup keys, where its Secure Boot setting is, laptop or desktop, how much space to give
  Linux, and which extra setup steps apply (like "NVIDIA graphics"). Never part ID numbers,
  device lists, your apps, your PC's model or serial number, or your name.
- **Your list of apps is never in the phone guide.** It's only in the copy you print or save.



These are planned and will follow the same rules. This policy will be updated before they ship.

| When | Sent to | What's sent |
|---|---|---|
| **Downloading Linux** (you click Download) | The distro's official download servers | A normal file download. Nothing about your PC. |
| **Sharing a part's results to improve GreyHaven** (only if you choose to) | GitHub, as a public issue, in your browser | Only part ID numbers and the type of part. You see exactly what will be posted, and you post it yourself, from your own GitHub account. It's public. |
| **Checking for a GreyHaven update** | GitHub | A request for the latest version number. Nothing about your PC. |

## What GreyHaven keeps on your PC

- **Settings** (light or dark, and the online check on or off) in
  `%LOCALAPPDATA%\GreyHaven\settings.json`.
- **Saved answers from linux-hardware.org**, so the same part isn't looked up twice, in
  `%LOCALAPPDATA%\GreyHaven\cache`.
- **Your guide**, in `Documents\GreyHaven`, so you can open it from Linux before installing.
  It's yours to delete.
- **Reports you save**, only where you choose to save them. A report lists your PC's part IDs and
  Windows settings, but none of the things GreyHaven never reads (above), so it's safe to share
  when asking for help.

Uninstalling GreyHaven and deleting the `%LOCALAPPDATA%\GreyHaven` folder removes everything.

## Children

GreyHaven doesn't collect personal information from anyone, including children.

## Changes and questions

Changes to this policy are listed in the project's history on GitHub. Questions: open an issue
on GreyHaven's GitHub page.
