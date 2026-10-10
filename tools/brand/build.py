#!/usr/bin/env python3
"""GreyHaven's brand assets, drawn from code so they stay consistent and reproducible.

    python3 tools/brand/build.py

Writes assets/brand/: the mark (SVG), app icon sizes (PNG) and greyhaven.ico, the README
banner (light and dark), the GitHub social preview and the download page's images.
Needs rsvg-convert (librsvg). Text uses the app's own fonts from assets/fonts (OFL).
See docs/brand.md for how to use them.
"""
import os, struct, subprocess, tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "assets" / "brand"

# Market Day palette (src/GreyHaven.Presentation/Theming/Palette.cs).
INK, PAPER, TOMATO, BUTTER, LEAF, CARD = "#1F1B16", "#F3EDE0", "#B2361A", "#FFC93C", "#2B6630", "#FFFFFF"
NIGHT, NIGHT_CARD, NIGHT_TEXT, NIGHT_LINE, EMBER = "#0D1218", "#18212B", "#F3EFE7", "#8C9BAB", "#FF9065"


def mark(size=256, shadow=True, x=0, y=0, scale=1.0, ink=INK, simple=False):
    """The mark: a lighthouse on a tomato sticker tile. A haven that guides you in: GreyHaven
    checks the way, then lights each step. `simple` drops the light beams for tiny icons."""
    g = []
    if shadow:
        g.append(f'<rect x="28" y="28" width="212" height="212" rx="52" fill="{ink}"/>')
    t0, tw = (16, 212) if shadow else (10, 236)
    g.append(f'<rect x="{t0}" y="{t0}" width="{tw}" height="{tw}" rx="52" fill="{TOMATO}" stroke="{ink}" stroke-width="14"/>')
    cx = t0 + tw / 2
    k = tw / 212 * (1.3 if simple else 1)  # tiny icons: a bigger lighthouse, so it stays readable
    def X(v): return cx + v * k
    def Y(v): return t0 + tw / 2 + (v - 106) * k
    sw = 10 * k
    if not simple:  # light beams, behind the lamp
        g.append(f'<path d="M{X(0)} {Y(64)} L{X(-92)} {Y(36)} L{X(-92)} {Y(84)} Z M{X(0)} {Y(64)} L{X(92)} {Y(36)} L{X(92)} {Y(84)} Z" fill="{BUTTER}" opacity="0.9"/>')
    # Tower: cream, tapering, with one tomato band.
    g.append(f'<path d="M{X(-34)} {Y(178)} L{X(-22)} {Y(86)} L{X(22)} {Y(86)} L{X(34)} {Y(178)} Z" fill="{PAPER}" stroke="{ink}" stroke-width="{sw}" stroke-linejoin="round"/>')
    g.append(f'<path d="M{X(-28.5)} {Y(136)} L{X(-26)} {Y(116)} L{X(26)} {Y(116)} L{X(28.5)} {Y(136)} Z" fill="{TOMATO}" stroke="{ink}" stroke-width="{sw*0.8}" stroke-linejoin="round"/>')
    # Lamp room (butter yellow) and roof.
    g.append(f'<rect x="{X(-24)}" y="{Y(52)}" width="{48*k}" height="{34*k}" rx="{4*k}" fill="{BUTTER}" stroke="{ink}" stroke-width="{sw}"/>')
    g.append(f'<path d="M{X(-32)} {Y(54)} L{X(0)} {Y(28)} L{X(32)} {Y(54)} Z" fill="{ink}" stroke="{ink}" stroke-width="{sw*0.8}" stroke-linejoin="round"/>')
    # Ground.
    g.append(f'<line x1="{X(-62)}" y1="{Y(180)}" x2="{X(62)}" y2="{Y(180)}" stroke="{ink}" stroke-width="{sw*1.2}" stroke-linecap="round"/>')
    return f'<g transform="translate({x} {y}) scale({scale})">{"".join(g)}</g>'


def svg(w, h, body, bg=None):
    rect = f'<rect width="{w}" height="{h}" fill="{bg}"/>' if bg else ""
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">{rect}{body}</svg>'


def text(x, y, value, size, weight=700, family="Rubik", fill=INK, anchor="start"):
    return (f'<text x="{x}" y="{y}" font-family="{family}" font-weight="{weight}" font-size="{size}" fill="{fill}" '
            f'text-anchor="{anchor}">{value}</text>')


def render(svg_text, png, width=None):
    with tempfile.TemporaryDirectory() as d:
        conf = Path(d) / "fonts.conf"
        conf.write_text(f'<?xml version="1.0"?><fontconfig><dir>{ROOT / "assets" / "fonts"}</dir>'
                        f'<cachedir>{d}/cache</cachedir></fontconfig>')  # only the app's fonts, so nothing falls back silently
        src = Path(d) / "in.svg"
        src.write_text(svg_text)
        args = ["rsvg-convert", str(src), "-o", str(png)] + (["-w", str(width)] if width else [])
        subprocess.run(args, check=True, env={**os.environ, "FONTCONFIG_FILE": str(conf)})


def ico(pngs, path):
    """A .ico holding PNG images (Windows Vista and later read these)."""
    data = [p.read_bytes() for p in pngs]
    sizes = [int(p.stem.split("-")[-1]) for p in pngs]
    head = struct.pack("<HHH", 0, 1, len(data))
    offset = 6 + 16 * len(data)
    entries, blobs = b"", b""
    for size, blob in zip(sizes, data):
        entries += struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
        blobs += blob
    path.write_bytes(head + entries + blobs)


def banner(dark):
    w, h = 1280, 320
    bg, fg, sub, line = (NIGHT, NIGHT_TEXT, "#B8C3CE", NIGHT_LINE) if dark else (PAPER, INK, "#52473B", INK)
    body = mark(x=64, y=48, scale=0.875, ink="#05080B" if dark else INK)
    body += text(320, 150, "GreyHaven", 96, 700, fill=fg)
    body += text(324, 214, "Will Linux work on your PC? Find out, then switch step by step.", 28, 400, "Lexend", sub)
    body += text(324, 262, "Free · Open source · Windows 10 and 11", 24, 400, "Lexend", sub)
    return svg(w, h, body, bg)


def social():
    """GitHub's social preview: 1280x640, important content inside a 40 px safe margin."""
    w, h = 1280, 640
    body = f'<rect x="72" y="88" width="1148" height="476" rx="40" fill="{INK}"/>'
    body += f'<rect x="56" y="72" width="1148" height="476" rx="40" fill="{CARD}" stroke="{INK}" stroke-width="8"/>'
    body += mark(x=112, y=168, scale=1.1)
    body += text(430, 272, "GreyHaven", 112, 700)
    body += text(434, 344, "Will Linux work on your PC?", 40, 400, "Lexend", "#52473B")
    body += text(434, 400, "Check it, make the USB stick, switch.", 40, 400, "Lexend", "#52473B")
    body += f'<rect x="434" y="440" width="300" height="56" rx="12" fill="{LEAF}" stroke="{INK}" stroke-width="4"/>'
    body += text(584, 478, "Free and open source", 24, 600, "Lexend", CARD, "middle")
    return svg(w, h, body, PAPER)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "mark.svg").write_text(svg(256, 256, mark()) + "\n")
    (OUT / "mark-flat.svg").write_text(svg(256, 256, mark(shadow=False)) + "\n")
    icons = []
    for size in (16, 24, 32, 48, 64, 128, 256):
        p = OUT / f"icon-{size}.png"
        # Small sizes drop the shadow, so the tile fills the square and stays crisp.
        render(svg(256, 256, mark(shadow=size >= 48, simple=size <= 32)), p, size)
        icons.append(p)
    ico(icons, OUT / "greyhaven.ico")
    render(banner(False), OUT / "banner-light.png", 1280)
    render(banner(True), OUT / "banner-dark.png", 1280)
    render(social(), OUT / "social-preview.png", 1280)
    print("Wrote", ", ".join(sorted(p.name for p in OUT.iterdir())))


if __name__ == "__main__":
    main()
