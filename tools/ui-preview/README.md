# Design preview (throwaway)

A browser mock-up of the GreyHaven app's flow, for reviewing the design without a Windows PC:
Scan my PC → score → Easy/Advanced choice → distro comparison → part details → Save report.
It is **not the real app**, isn't built into the app, installer or releases, and says so in a
banner on every page.

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
