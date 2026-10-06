# Design preview (throwaway)

A browser mock-up of the GreyHaven app's flow, for reviewing the design without a Windows PC:
Scan my PC → score → Easy/Advanced choice → distro comparison → part details → Save report.
It is **not the real app**, isn't built into the app, installer or releases, and says so in a
banner on every page.

Round 2 (2026-10-06) tries out brand directions (`brands.json`: palettes, fonts, picture
style) and plain, beginner-level wording. Switch with **Look** in the preview's yellow bar.
The build checks every brand against WCAG AA (text 4.5:1, borders 3:1) in light and dark mode
and stops if one fails. `?counts=1` shows the words on screen and a rough reading level.

Round 3 (2026-10-06) adds the guided "What's next" walkthrough (`walkthrough.js`): nine steps,
one per screen, in the chosen Market Day look. Downloading and writing the USB stick are
simulated (milestone 6). Makers' boot keys and support pages, and free disk space, are
preview-only data marked in the file; they move into the scan and `data/` when the design is
applied. Shortcuts: `?screen=walk&step=8&install=alongside`, `?screen=allsteps`.

It uses real data and the app's own design sources:

- reports for every sample, from the GreyHaven CLI (`rate --json`)
- colours generated from `src/GreyHaven.Presentation/Theming/Palette.cs`
- labels generated from `src/GreyHaven.Core/Hardware/Labels.cs`
- layout, sizes and wording copied from the WPF views and view models

When those change, rebuild. When the views change, update `app.js`/`app.css` by hand.

```sh
python3 -I tools/ui-preview/build.py --serve      # build into artifacts/preview, serve on :8765
```

Then open http://localhost:8765/. The index links the preview, HTML reports for every sample
and path, and the real app's screenshots from the latest successful CI run on `main` (needs
`gh`; inside a Flatpak it's run through `flatpak-spawn --host`; skip with `--no-screenshots`).

URL shortcuts: `ui-preview/index.html?sample=nvidia-desktop&screen=results&path=advanced&theme=dark&open=gpu`.
