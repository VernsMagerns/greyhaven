# SignPath Foundation application (draft)

*Draft answers for https://signpath.org/apply. Don't submit until the steps before it in
[going-public.md](going-public.md) are done: SignPath requires a public repository and an
already-published release. The form loads by script and couldn't be read automatically, so its
exact questions may differ; these answers cover everything SignPath's published conditions
(https://signpath.org/terms.html) ask for.*

| Question | Answer |
|---|---|
| Project name | GreyHaven |
| Repository | https://github.com/VernsMagerns/greyhaven |
| Homepage / download page | https://vernsmagerns.github.io/greyhaven/ (GitHub Pages, once live) |
| Licence | MIT (all code). The compatibility data in `data/` is CC-BY-4.0 (see open questions). |
| What it does | A Windows app that checks how well a PC's hardware will work with Linux (Linux Mint, Ubuntu, Pop!_OS), recommends one, and guides a beginner step by step through trying and installing it. |
| Platform and artifacts to sign | Windows x64: `GreyHaven-Setup.exe` (WiX Burn bundle), `GreyHaven.msi`, and the app's own `.exe` and `.dll` files. |
| Build system | GitHub Actions, from tagged commits on `main` only. |
| How often | A few releases a year, plus occasional data updates. |
| Team | One maintainer, @VernsMagerns, in all three roles: author/committer, reviewer, approver. MFA on GitHub and SignPath. |
| Code signing policy | https://github.com/VernsMagerns/greyhaven/blob/main/docs/code-signing-policy.md (also linked from the download page) |
| Privacy policy | https://github.com/VernsMagerns/greyhaven/blob/main/PRIVACY.md |
| Proprietary components | None written by the project. Uses .NET (MIT), WPF (MIT), CommunityToolkit.Mvvm (MIT), YamlDotNet (MIT), QRCoder (MIT), the Rubik and Lexend fonts (SIL OFL 1.1), and the WiX Toolset (see open questions). |
| Metadata | Product name "GreyHaven", version, and company "GreyHaven contributors" are set on every binary (`Directory.Build.props`). |
| Reputation | (Fill in at application time: release downloads, stars, links to discussions or reviews.) |

## Open questions to raise in the application

1. **CC-BY-4.0 data.** SignPath asks for an OSI-approved licence. The code is MIT; the curated
   compatibility data (`data/*.yaml`) is CC-BY-4.0, a Creative Commons licence for content,
   not software, and not on the OSI list. Ask whether that's acceptable for bundled data
   files, or whether to relicense `data/` under MIT too (we can, as the only author so far).
2. **WiX Toolset.** The Burn engine from WiX ends up inside `GreyHaven-Setup.exe`. WiX's source
   is open (MS-RL), but WiX 6+ binaries come with the "Open Source Maintenance Fee" agreement
   (no fee for projects earning under US$10,000 a year). Ask whether that counts as a
   proprietary component. Fallback if it does: build `GreyHaven-Setup.exe` with Inno Setup
   (open source) and keep the WiX MSI for admins, or ship the MSI alone.
3. **Single maintainer.** All three roles are one person for now. SignPath allows this for
   small projects but may ask how reviews work; answer: every outside pull request is reviewed
   before merging, and CI must pass on Linux and Windows.
