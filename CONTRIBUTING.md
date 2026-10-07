# Contributing to GreyHaven

Thanks for helping! GreyHaven helps people who've never used Linux find out whether their PC
will work with it, and guides them all the way through switching. Everything here serves that
person.

By taking part, you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Ways to help

### Tell us when GreyHaven gets a part wrong

The most useful help. If GreyHaven says a part works and it doesn't on Linux (or the other way
round), [open an issue](../../issues/new) with:

- the part's ID from the report's "Technical details" (like `pci:8086-2723`),
- which version of Linux you tried, and what happened,
- if you can, a link to a hardware report from `hw-probe` (the report's live-USB step explains how).

Please don't paste whole scan files publicly unless you've checked them: they contain only part
IDs and settings, but it's your choice what to share.

### Improve the compatibility data (no programming needed)

Ratings come from files in [data/](data/), not code. Read [data/README.md](data/README.md), then
edit a rule and open a pull request. The rules for rules:

- Use **part IDs, never names**. If you're not sure, don't add the rule: "Not sure" is better
  than a wrong answer.
- Link a **source you've checked** (kernel source, the distro's own docs or release notes).
- Write a short **`summary`** line in plain words (two sentences at most, under 15 words each).

### Code

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download). Everything except the live
   scan builds and runs on Linux, macOS and Windows.
2. `dotnet build` and `dotnet test`. Try the app's logic with the samples:
   `dotnet run --project src/GreyHaven.Cli -- rate --sample nvidia-desktop --offline`.
3. Keep pull requests small and focused, with tests.

## What we care about

- **Plain words.** Write for someone who has never done this, at about a 6th-grade reading
  level: short sentences, one idea at a time, details behind "Show more". Explain jargon the
  first time it appears.
- **Accessibility.** Every control has a name for screen readers, colours meet WCAG AA in light
  and dark mode, High Contrast keeps working, everything works with a keyboard. Tests check
  much of this; please keep them passing. No small all-caps labels above headings.
- **Safety.** GreyHaven only changes things on a PC when the user clearly asks, one action at a
  time, with a way back. Firmware and disk changes are instructions only (except the planned,
  carefully checked USB stick writing).
- **Privacy.** No tracking. Anything that sends data must be in [PRIVACY.md](PRIVACY.md) first.
- **No guessing.** No data means "Not sure", never a made-up rating.

## Reviews and releases

Changes reach `main` through pull requests. Pull requests from people outside the team need a
review from a maintainer before merging, and CI must pass on Linux and Windows. Releases are
built and signed only by CI; see [docs/code-signing-policy.md](docs/code-signing-policy.md).

## Licence

By contributing, you agree that your code is released under the [MIT licence](LICENSE) and your
changes to `data/` under [CC-BY-4.0](data/LICENSE).

## Security problems

Please report them privately: see [SECURITY.md](SECURITY.md).
