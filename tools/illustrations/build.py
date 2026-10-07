#!/usr/bin/env python3
"""Generates GreyHaven's walkthrough pictures in the "Market Day" style.

    python3 -I tools/illustrations/build.py

Each picture is a simplified drawing of a real screen: the same button labels, in the same
places, so people recognise what they'll see. No company or distro logos: names are text.
One description per picture produces:

    assets/illustrations/<id>.svg          for the HTML report (light/dark built in)
    assets/illustrations/catalog.json      alt text, caption hint and what it was checked against
    src/GreyHaven.App/Theming/Illustrations.xaml   the same drawings for the WPF app
    src/GreyHaven.App/Theming/IllustrationColours.g.cs   their light/dark colours

Pictures are decorative copies of what the step's sentence already says; the app hides them
in High Contrast, and the report hides them under forced colours (the caption stays).
"""
import json, re
from pathlib import Path
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "assets" / "illustrations"
W, H = 320, 200

# Colour roles (light, dark). Text roles meet WCAG AA on the surfaces they're drawn on.
COLOURS = {
    "page": ("#F3EDE0", "#0D1218"),     # desktop / paper behind windows
    "bg": ("#FFFFFF", "#18212B"),       # window
    "bar": ("#EEE6D5", "#111820"),      # title bar, side panels
    "hdr": ("#3B342C", "#0B0F14"),      # dark header band (Mint), dark window (Pop!_OS)
    "hdrtxt": ("#FFFFFF", "#F3EFE7"),
    "ink": ("#1F1B16", "#8C9BAB"),      # outlines
    "txt": ("#1F1B16", "#F3EFE7"),
    "mut": ("#52473B", "#B8C3CE"),
    "btn": ("#F3EDE0", "#24303C"),
    "pri": ("#2B6630", "#8FD69B"),      # the main button (Install, Restart...)
    "pritxt": ("#FFFFFF", "#0D1218"),
    "sel": ("#FFE08A", "#2D2616"),      # chosen row
    "acc": ("#B2361A", "#FF9065"),      # radio dot, tick box
    "acctxt": ("#FFFFFF", "#0D1218"),
    "hl": ("#B2361A", "#FF9065"),       # "click here" ring
    "blue": ("#1B4F8A", "#1B4F8A"),     # Windows' blue startup screens
    "bluetxt": ("#FFFFFF", "#FFFFFF"),
    "deco1": ("#FFC93C", "#FFC93C"),
    "deco2": ("#3E8A4A", "#5FAE6A"),
}

# ---- Drawing primitives (shared by SVG and XAML) --------------------------------------------

def R(x, y, w, h, fill="bg", stroke=None, rx=0, sw=1.5, dash=None):
    return {"t": "rect", "x": x, "y": y, "w": w, "h": h, "fill": fill, "stroke": stroke, "rx": rx, "sw": sw, "dash": dash}

def C(cx, cy, r, fill="bg", stroke=None, sw=1.5):
    return {"t": "circle", "cx": cx, "cy": cy, "r": r, "fill": fill, "stroke": stroke, "sw": sw}

def T(x, y, s, size=7, cls="txt", weight=400, anchor="start", width=None):
    return {"t": "text", "x": x, "y": y, "s": s, "size": size, "cls": cls, "weight": weight, "anchor": anchor, "width": width}

def P(d, fill=None, stroke=None, sw=1.5):
    return {"t": "path", "d": d, "fill": fill, "stroke": stroke, "sw": sw}

def button(x, y, w, label, kind="btn", h=14, target=False, size=6.5):
    fill, tc = {"btn": ("btn", "txt"), "pri": ("pri", "pritxt"), "acc": ("acc", "acctxt")}[kind]
    out = [R(x, y, w, h, fill, "ink", rx=3, sw=1), T(x + w / 2, y + h / 2 + size * 0.36, label, size, tc, 600, "middle", w)]
    if target: out += ring(x, y, w, h)
    return out

def ring(x, y, w, h, pad=3):
    """The tomato "click here" ring and a little pointer."""
    px, py = x + w - 4, y + h - 2
    return [R(x - pad, y - pad, w + 2 * pad, h + 2 * pad, None, "hl", rx=5, sw=2.2),
            P(f"M{px} {py} l0 11 l3 -3 l2.5 5 l2 -1 l-2.5 -5 l4.2 0 z", "bg", "ink", 1)]

def radio(x, y, label, sub=None, on=False, w=150, target=False, size=6.5, box=True):
    out = []
    h = 24 if sub else 15
    if box: out.append(R(x, y, w, h, "sel" if on else "bg", "ink", rx=3, sw=1))
    out.append(C(x + 8, y + 7.5, 3.4, "bg", "ink", 1))
    if on: out.append(C(x + 8, y + 7.5, 1.9, "acc"))
    out.append(T(x + 15, y + 9.6, label, size, "txt", 600))
    if sub: out.append(T(x + 15, y + 18.5, sub, size - 1.2, "mut"))
    if target: out += ring(x, y, w, h)
    return out

def check(x, y, label, on=True, sub=None, target=False, size=6.5, w=None):
    out = [R(x, y + 2, 7.5, 7.5, "acc" if on else "bg", "ink", rx=1.5, sw=1)]
    if on: out.append(P(f"M{x+1.6} {y+6} l2 2 l3.6 -4", None, "acctxt", 1.4))
    out.append(T(x + 11, y + 8.4, label, size, "txt", 600))
    if sub: out.append(T(x + 11, y + 16.5, sub, size - 1.2, "mut"))
    if target: out += ring(x, y, w or (11 + len(label) * size * 0.52), 11)
    return out

def listbox(x, y, w, items, chosen=None, row=12, size=6, target=False):
    out = [R(x, y, w, row * len(items) + 2, "bg", "ink", rx=3, sw=1)]
    for i, it in enumerate(items):
        if i == chosen: out.append(R(x + 1, y + 1 + i * row, w - 2, row, "sel"))
        out.append(T(x + 6, y + 1 + i * row + row * 0.68, it, size, "txt", 600 if i == chosen else 400))
    if target and chosen is not None: out += ring(x + 1, y + 1 + chosen * row, w - 2, row, pad=1.5)
    return out

def desk_bg():
    return [R(0, 0, W, H, "page")]

def blob(x, y, s=1):
    """A small Market Day picture in the left panel of installer windows (no logos)."""
    return [R(x - 22 * s, y - 14 * s, 44 * s, 28 * s, "deco1", "ink", rx=5 * s, sw=1.2),
            C(x + 20 * s, y + 12 * s, 12 * s, "deco2", "ink", 1.2),
            P(f"M{x+14*s} {y+12*s} l4 4 l7 -8", None, "acctxt", 1.8)]

# ---- Window templates -----------------------------------------------------------------------

def ubuntu_window(title, heading, body, next_label="Next", next_kind="btn", back=True, dots=None):
    """Ubuntu's installer: centred title, picture on the left, Back / page dots / Next."""
    out = desk_bg() + [R(12, 10, 296, 180, "bg", "ink", rx=6), R(12, 10, 296, 15, "bar", None, rx=6), R(12, 20, 296, 5, "bar"),
                       T(160, 20.5, title, 6.2, "txt", 600, "middle", 200), blob(70, 95, 1.1)]
    out = flatten(out)
    out.append(T(130, 46, heading, min(8, 165 / (len(heading) * 0.56)), "txt", 700))  # shrink long headings to fit
    out += body
    if back: out += button(22, 166, 44, "Back")
    for i in range(dots or 12):
        out.append(C(130 + i * 6, 173, 1.6, "acc" if i == (dots or 12) // 2 else "mut"))
    return out, next_label, next_kind

def ubuntu_screen(title, heading, body, next_label="Next", next_kind="btn", target_next=True, back=True):
    out, label, kind = ubuntu_window(title, heading, body, next_label, next_kind, back)
    out += button(254, 166, 44, label, kind, target=target_next)
    return out

def mint_screen(page_title, body, buttons=("Quit", "Back", "Continue"), target=-1):
    """Linux Mint's installer: 'Install' title bar, dark header with the page title, buttons bottom right."""
    out = desk_bg() + [R(12, 8, 296, 184, "bg", "ink", rx=5), R(12, 8, 296, 13, "bar", None, rx=5), R(12, 16, 296, 5, "bar"),
                       T(160, 17.5, "Install", 6.2, "txt", 600, "middle", 100), R(13.5, 21, 293, 22, "hdr"),
                       T(22, 36, page_title, 9, "hdrtxt", 400), R(13.5, 166, 293, 24.5, "hdr")]
    for i in range(7): out.append(R(140 + i * 7, 175, 4, 4, "deco1" if i < 4 else "hdrtxt"))
    out += body
    x = 302
    for i, b in reversed(list(enumerate(buttons))):
        w = max(34, len(b) * 4.2 + 12)
        x -= w + 4
        out += button(x, 146, w, b, target=(i == (len(buttons) + target) % len(buttons)))
    return out

def pop_screen(left_caption, body, buttons, target_index, try_demo=False):
    """Pop!_OS's installer: dark window, round picture with a caption on the left, buttons bottom right."""
    out = desk_bg() + [R(12, 8, 296, 184, "hdr", "ink", rx=6), T(160, 19, "Install Pop!_OS 24.04 LTS", 6.2, "hdrtxt", 600, "middle", 200),
                       C(80, 82, 38, "bar", "ink", 1.2)] + blob(80, 82, 0.9) + [T(80, 140, left_caption, 9, "hdrtxt", 400, "middle", 130)]
    out += body
    x = 302
    for i, (b, kind) in reversed(list(enumerate(buttons))):
        w = max(40, len(b) * 4.3 + 12)
        x -= w + 4
        out += button(x, 170, w, b, kind, target=(i == target_index))
    if try_demo: out += button(22, 170, 56, "Try Demo Mode")
    return out

def flatten(items):
    out = []
    for i in items:
        if isinstance(i, list): out += flatten(i)
        else: out.append(i)
    return out

def win_settings(win11, nav, title, rows, target_row=None):
    """Windows Settings: left navigation, page title, rows with a button on the right."""
    out = desk_bg() + [R(10, 8, 300, 184, "bg", "ink", rx=6), R(10, 8, 300, 13, "bar", None, rx=6), R(10, 15, 300, 6, "bar"),
                       T(18, 17.5, "Settings", 6, "txt", 600), R(10, 21, 82, 170.5, "bar")]
    for i, n in enumerate(nav):
        y = 34 + i * 13
        if n.startswith("*"):
            n = n[1:]
            out += [R(14, y - 8, 74, 12, "sel", None, rx=3), R(14, y - 6, 2, 8, "acc")]
        out.append(T(20, y, n, 5.8, "txt"))
    out.append(T(102, 38, title, min(10, 200 / (len(title) * 0.6)), "txt", 700))
    for i, (label, sub, btn) in enumerate(rows):
        y = 50 + i * 30
        out += [R(100, y, 200, 26, "bar", None, rx=4), T(108, y + 11, label, 6.5, "txt", 600)]
        if sub: out.append(T(108, y + 20, sub, 5, "mut"))
        if btn: out += button(250, y + 6, 44, btn, target=(i == target_row))
    return out

def gtk_window(title, body, subtitle=None):
    """A Linux app window (Software & Updates, Driver Manager)."""
    out = desk_bg() + [R(12, 8, 296, 184, "bg", "ink", rx=6), R(12, 8, 296, 20 if subtitle else 15, "bar", None, rx=6),
                       T(160, 17 if subtitle else 18.5, title, 6.4, "txt", 700, "middle", 200)]
    if subtitle: out.append(T(160, 24.5, subtitle, 5, "mut", 400, "middle", 200))
    return out + body

def dialog(title, lines, buttons, target, icon=None):
    out = desk_bg() + [R(40, 50, 240, 100, "bg", "ink", rx=6), R(40, 50, 240, 15, "bar", None, rx=6), R(40, 58, 240, 7, "bar"),
                       T(160, 60.5, title, 6.4, "txt", 600, "middle", 200)]
    for i, l in enumerate(lines): out.append(T(56, 84 + i * 10, l, 6.4, "txt", 700 if i == 0 and icon == "bold" else 400))
    x = 270
    for i, (b, kind) in reversed(list(enumerate(buttons))):
        w = max(40, len(b) * 4.3 + 12)
        x -= w + 5
        out += button(x, 128, w, b, kind, target=(i == target))
    return out

# ---- The pictures ---------------------------------------------------------------------------
# Each entry: id -> (elements, alt text, what it was checked against)

UBU = "Ubuntu Desktop installer, from the official 'Install Ubuntu Desktop' tutorial screenshots (24.04 layout; check against 26.04)"
MINT = "Linux Mint installer, from the official Linux Mint Installation Guide screenshots"
POP = "Pop!_OS 24.04 installer, from System76's 'Install Pop!_OS' article screenshots"
WIN = "Windows Settings and startup screens (layout from Microsoft's documentation)"

def ubuntu_scenes():
    s = {}
    s["ubuntu-language"] = (ubuntu_screen("Welcome", "Welcome to Ubuntu", listbox(130, 54, 160, ["Deutsch", "English", "Español", "Français", "Italiano", "Nederlands"], 1, target=True), target_next=False, back=False),
        "Ubuntu installer window titled Welcome, with a list of languages, English chosen, and a Next button at the bottom right.", UBU)
    s["ubuntu-keyboard"] = (ubuntu_screen("Keyboard layout", "Select your keyboard layout", listbox(130, 54, 160, ["English (UK)", "English (US)", "French", "German"], 1) + [R(130, 108, 160, 14, "bg", "ink", rx=3, sw=1), T(135, 117, "Type here to test your keyboard", 5.5, "mut")]),
        "Ubuntu installer Keyboard layout screen with English (US) chosen and the Next button highlighted.", UBU)
    s["ubuntu-internet"] = (ubuntu_screen("Internet connection", "Connect to the internet", radio(130, 56, "Use wired connection", on=True, box=False) + radio(130, 72, "Connect to a Wi-Fi network", box=False) + radio(130, 88, "Do not connect to the internet", box=False)),
        "Ubuntu installer Connect to the internet screen with three choices and the Next button highlighted.", UBU)
    s["ubuntu-try-install"] = (ubuntu_screen("Try or install Ubuntu", "What do you want to do with Ubuntu?", radio(130, 56, "Install Ubuntu", "Install alongside (or instead of) your OS.", on=True, w=165, target=True) + radio(130, 86, "Try Ubuntu", "Try it without making any changes.", w=165), target_next=False),
        "Ubuntu installer screen 'What do you want to do with Ubuntu?' with Install Ubuntu chosen.", UBU)
    s["ubuntu-interactive"] = (ubuntu_screen("Type of installation", "How would you like to install Ubuntu?", radio(130, 56, "Interactive installation", "Guided step by step.", on=True, w=165, target=True) + radio(130, 86, "Automated installation", "For advanced users.", w=165), target_next=False),
        "Ubuntu installer screen with Interactive installation chosen.", UBU)
    s["ubuntu-apps"] = (ubuntu_screen("Applications", "What apps would you like to install?", radio(130, 56, "Default selection", "Just the essentials.", on=True, w=165) + radio(130, 86, "Extended selection", "Office tools and more.", w=165)),
        "Ubuntu installer Applications screen with Default selection chosen and Next highlighted.", UBU)
    s["ubuntu-third-party"] = (ubuntu_screen("Optimise your computer", "Install recommended proprietary software?", check(130, 58, "Third-party software for graphics and Wi-Fi", w=160, target=True, size=5.8) + check(130, 78, "Support for additional media formats", size=5.8), target_next=False),
        "Ubuntu installer Optimise your computer screen with both boxes ticked: third-party software for graphics and Wi-Fi, and support for additional media formats.", UBU)
    s["ubuntu-alongside"] = (ubuntu_screen("Disk setup", "How do you want to install Ubuntu?", radio(130, 54, "Install Ubuntu alongside Windows Boot Manager", box=False, on=True, size=5.6, target=True, w=165) + radio(130, 72, "Erase disk and install Ubuntu", box=False, size=5.6) + radio(130, 90, "Manual installation", box=False, size=5.6), target_next=False),
        "Ubuntu installer Disk setup screen with 'Install Ubuntu alongside Windows Boot Manager' chosen.", UBU + "; the alongside wording is from third-party 24.04 guides")
    s["ubuntu-divider"] = (ubuntu_screen("Disk setup", "Make room for Ubuntu", [R(130, 64, 160, 18, "bar", "ink", rx=3, sw=1), R(131, 65, 96, 16, "sel"), T(136, 76, "Windows", 6, "txt", 600), T(234, 76, "Ubuntu", 6, "txt", 600), R(226, 60, 4, 26, "acc", "ink", rx=2, sw=1)] + ring(222, 60, 12, 26) + [T(130, 100, "Drag the slider to share the space.", 5.6, "mut")], target_next=False),
        "Ubuntu installer screen with a bar split between Windows and Ubuntu and a slider between them to drag.", UBU + "; slider layout from third-party 24.04 guides")
    s["ubuntu-erase"] = (ubuntu_screen("Disk setup", "How do you want to install Ubuntu?", radio(130, 58, "Erase disk and install Ubuntu", "Start from scratch on your disk.", on=True, box=False, target=True, w=160) + button(145, 84, 58, "Advanced features...", size=5) + radio(130, 104, "Manual installation", box=False), target_next=False),
        "Ubuntu installer Disk setup screen with 'Erase disk and install Ubuntu' chosen.", UBU)
    s["ubuntu-account"] = (ubuntu_screen("Create your account", "Create your account", flatten([[R(130, 52 + i * 15, 160, 11, "bg", "ink", rx=2, sw=1), T(134, 59.5 + i * 15, l, 5.4, "mut")] for i, l in enumerate(["Your name", "Your computer's name", "Your username", "Password", "Confirm password"])])),
        "Ubuntu installer Create your account screen with boxes for your name, computer name, username and password, and Next highlighted.", UBU)
    s["ubuntu-timezone"] = (ubuntu_screen("Select your timezone", "Where are you?", [R(130, 52, 160, 70, "bar", "ink", rx=3, sw=1), P("M140 80 q20 -18 40 -4 q16 10 30 -2 q20 -12 36 6 q10 10 30 2", None, "mut", 1.2), C(196, 84, 3, "acc")]),
        "Ubuntu installer time zone screen with a world map and a dot on your location.", UBU)
    s["ubuntu-review"] = (ubuntu_screen("Ready to install", "Review your choices", [R(130, 52, 160, 76, "bar", "ink", rx=3, sw=1), T(136, 64, "Disk setup", 5.8, "txt"), T(286, 64, "Erase disk and install", 5.8, "txt", 600, "end"), T(136, 76, "Installation disk", 5.8, "txt"), T(286, 76, "nvme0n1", 5.8, "txt", 600, "end"), T(136, 88, "Applications", 5.8, "txt"), T(286, 88, "Default selection", 5.8, "txt", 600, "end")], next_label="Install", next_kind="pri"),
        "Ubuntu installer Review your choices screen listing the disk setup, with a green Install button at the bottom right.", UBU)
    out, _, _ = ubuntu_window("Installation complete", "Ubuntu is installed and ready to use", [], back=False)
    s["ubuntu-restart"] = (out + button(156, 120, 62, "Continue testing") + button(224, 120, 56, "Restart now", "pri", target=True),
        "Ubuntu installer Installation complete screen with Continue testing and a green Restart now button.", UBU)
    return s

def mint_scenes():
    s = {}
    s["mint-install-icon"] = (desk_bg() + [R(0, 0, W, H, "hdr"), C(42, 40, 12, "btn", "ink"), C(42, 40, 3, "ink"), T(42, 66, "Install Linux Mint", 6.4, "hdrtxt", 600, "middle", 80)] + ring(18, 24, 48, 48) + [R(0, 186, W, 14, "bar")],
        "The Linux Mint desktop with an 'Install Linux Mint' disc icon at the top left.", MINT)
    s["mint-language"] = (mint_screen("Welcome", listbox(22, 50, 120, ["Deutsch", "English", "Español", "Français", "Italiano", "Nederlands"], 1)),
        "Linux Mint installer Welcome screen with a list of languages, English chosen, and Quit, Back and Continue buttons with Continue highlighted.", MINT)
    s["mint-keyboard"] = (mint_screen("Keyboard layout", listbox(22, 50, 120, ["English (UK)", "English (US)", "French", "German"], 1) + listbox(150, 50, 150, ["English (US)", "English (US) - Cherokee", "English (US) - Colemak"], 0)),
        "Linux Mint installer Keyboard layout screen with English (US) chosen and Continue highlighted.", MINT)
    s["mint-wifi"] = (mint_screen("Wireless", [T(22, 58, "Connecting this computer to a Wi-Fi network:", 6.2, "txt")] + radio(22, 64, "I don't want to connect to a Wi-Fi network now", box=False, size=5.8) + radio(22, 80, "Connect to this network", on=True, box=False, size=5.8) + listbox(40, 96, 150, ["HomeNetwork", "Neighbour-5G"], 0)),
        "Linux Mint installer Wireless screen with a home Wi-Fi network chosen and Continue highlighted.", MINT)
    s["mint-codecs"] = (mint_screen("Multimedia codecs", check(22, 52, "Install multimedia codecs", target=True) + [T(33, 70, "Codecs are required to play some video formats.", 5.4, "mut")], target=-1),
        "Linux Mint installer Multimedia codecs screen with 'Install multimedia codecs' ticked and Continue highlighted.", MINT)
    s["mint-alongside"] = (mint_screen("Installation type", [T(22, 56, "This computer currently has Windows Boot Manager on it.", 5.8, "txt")] + radio(22, 62, "Install Linux Mint alongside Windows Boot Manager", "Documents and files will be kept.", on=True, box=False, size=5.8, target=True, w=210) + radio(22, 90, "Erase disk and install Linux Mint", box=False, size=5.8) + radio(22, 106, "Something else", box=False, size=5.8)),
        "Linux Mint installer Installation type screen with 'Install Linux Mint alongside Windows Boot Manager' chosen.", MINT)
    s["mint-divider"] = (mint_screen("Installation type", [T(22, 58, "Allocate drive space by dragging the divider below:", 6, "txt"), R(22, 70, 270, 20, "bar", "ink", rx=3, sw=1), R(23, 71, 160, 18, "sel"), T(30, 83, "Windows", 6, "txt", 600), T(196, 83, "Linux Mint", 6, "txt", 600), R(182, 66, 4, 28, "acc", "ink", rx=2, sw=1)] + ring(178, 66, 12, 28), buttons=("Quit", "Back", "Install Now")),
        "Linux Mint installer screen with a bar shared between Windows and Linux Mint, a divider to drag, and an Install Now button.", MINT)
    s["mint-erase"] = (mint_screen("Installation type", [T(22, 56, "This computer currently has Windows Boot Manager on it.", 5.8, "txt")] + radio(22, 62, "Install Linux Mint alongside Windows Boot Manager", box=False, size=5.8) + radio(22, 78, "Erase disk and install Linux Mint", "Warning: this deletes all your files.", on=True, box=False, size=5.8, target=True, w=180) + radio(22, 106, "Something else", box=False, size=5.8)),
        "Linux Mint installer Installation type screen with 'Erase disk and install Linux Mint' chosen and its warning.", MINT)
    s["mint-erase-check"] = (dialog("Write the changes to disks?", ["The partitions of the following device", "will be formatted: your drive.", "Check it's the right one."], [("Go Back", "btn"), ("Continue", "btn")], 1),
        "Linux Mint's 'Write the changes to disks?' box with Go Back and Continue buttons, Continue highlighted.", MINT)
    s["mint-timezone"] = (mint_screen("Where are you?", [R(22, 50, 276, 80, "bar", "ink", rx=3, sw=1), P("M40 90 q30 -24 60 -6 q30 14 60 -2 q40 -18 70 8 q20 12 50 0", None, "mut", 1.2), C(150, 94, 3, "acc"), R(22, 134, 120, 9, "bg", "ink", rx=2, sw=1), T(26, 140.5, "London", 5.4, "txt")]),
        "Linux Mint installer Where are you? screen with a world map and your city, and Continue highlighted.", MINT)
    s["mint-account"] = (mint_screen("Who are you?", flatten([[T(96, 59 + i * 15, l, 5.6, "txt", 400, "end", 80), R(100, 51 + i * 15, 130, 11, "bg", "ink", rx=2, sw=1)] for i, l in enumerate(["Your name:", "Your computer's name:", "Pick a username:", "Choose a password:", "Confirm your password:"])])),
        "Linux Mint installer Who are you? screen with boxes for your name, computer name, username and password, and Continue highlighted.", MINT)
    s["mint-restart"] = (dialog("Installation Complete", ["Installation has finished. You can continue", "testing Linux Mint now, but changes won't", "be kept until you restart."], [("Continue Testing", "btn"), ("Restart Now", "btn")], 1),
        "Linux Mint's Installation Complete box with Continue Testing and Restart Now buttons, Restart Now highlighted.", MINT)
    return s

def pop_scenes():
    s = {}
    s["pop-os-language"] = (pop_screen("Select Language", listbox(150, 50, 150, ["Deutsch", "English", "Español", "Français", "Italiano"], 1, size=6), [("Select", "pri")], 0),
        "Pop!_OS installer Select Language screen with English chosen and the Select button highlighted.", POP)
    s["pop-os-keyboard"] = (pop_screen("Keyboard Layout", listbox(150, 50, 150, ["English (UK)", "English (US)", "French", "German"], 1, size=6), [("Back", "btn"), ("Select", "pri")], 1),
        "Pop!_OS installer Keyboard Layout screen with English (US) chosen and Select highlighted.", POP)
    s["pop-os-clean-install"] = (pop_screen("Install", radio(150, 58, "Clean Install", "Erase everything and install.", on=True, w=150) + radio(150, 90, "Custom (Advanced)", "Manage partitions manually.", w=150), [("Back", "btn"), ("Clean Install", "pri")], 1, try_demo=True),
        "Pop!_OS installer Install screen with Clean Install chosen and the Clean Install button highlighted at the bottom right.", POP)
    s["pop-os-erase"] = (pop_screen("Select a drive", [R(170, 70, 110, 26, "sel", "ink", rx=3, sw=1), R(176, 75, 12, 16, "btn", "ink", rx=2, sw=1), T(194, 82, "Your drive", 6.2, "txt", 600), T(194, 91, "500 GB", 5.4, "mut")], [("Cancel Installation", "btn"), ("Erase and Install", "acc")], 1),
        "Pop!_OS installer Select a drive screen with your drive chosen and the red Erase and Install button highlighted.", POP)
    s["pop-os-account"] = (pop_screen("Create User Account", flatten([[R(160, 60 + i * 20, 130, 12, "bg", "ink", rx=2, sw=1), T(164, 68 + i * 20, l, 5.6, "mut")] for i, l in enumerate(["Full Name", "User Name"])]), [("Back", "btn"), ("Next", "pri")], 1),
        "Pop!_OS installer Create User Account screen with Full Name and User Name boxes and the Next button highlighted.", POP)
    s["pop-os-encryption"] = (pop_screen("Drive Encryption", [T(150, 64, "Encrypting protects your files if", 5.8, "hdrtxt"), T(150, 74, "someone takes the device.", 5.8, "hdrtxt")], [("Don't Encrypt", "btn"), ("Set Password", "pri")], 0),
        "Pop!_OS installer Drive Encryption screen with Don't Encrypt and Set Password buttons, Don't Encrypt highlighted.", POP)
    s["pop-os-restart"] = (pop_screen("Continue Setting Up", [], [("Restart Device", "pri"), ("Shut Down", "btn")], 0),
        "Pop!_OS installer Continue Setting Up screen with the Restart Device button highlighted.", POP)
    return s

def startup_settings(crumb, rows, target, choices=None):
    """A motherboard's startup settings (BIOS) screen: a path at the top, settings with their
    values, and optionally the little list that opens to change one. Drawn generically (no logos)."""
    out = desk_bg() + [R(12, 8, 296, 184, "hdr", "ink", rx=6), R(12, 8, 296, 18, "bar", None, rx=6), R(12, 18, 296, 8, "bar"),
                       T(22, 20.5, crumb, 6.4, "txt", 700, "start", 270)]
    for i, (name, value) in enumerate(rows):
        y = 40 + i * 20
        on = i == target
        if on: out.append(R(24, y - 2, 272, 16, "sel", None, rx=2))
        out += [T(32, y + 9, name, 6.6, "txt" if on else "hdrtxt", 700 if on else 400), T(290, y + 9, f"[{value}]" if value else ">", 6.6, "txt" if on else "hdrtxt", 700 if on else 400, "end", 90)]
        if on and not choices: out += ring(24, y - 2, 272, 16, pad=1.5)
    if choices:
        values, chosen = choices
        y0 = 40 + target * 20 + 18
        out.append(R(196, y0, 100, len(values) * 14 + 4, "bg", "ink", rx=3, sw=1))
        for j, v in enumerate(values):
            if j == chosen: out.append(R(198, y0 + 2 + j * 14, 96, 14, "sel", None, rx=2))
            out.append(T(206, y0 + 12 + j * 14, v, 6.4, "txt", 700 if j == chosen else 400, "start", 84))
        out += ring(198, y0 + 2 + chosen * 14, 96, 14, pad=1.5)
    out.append(T(160, 182, "F10: Save and restart", 6, "hdrtxt", 400, "middle", 200))
    return out

BIOS_MSI = "MSI Click BIOS 5 menu path (Settings > Security > Secure Boot), from search results; MSI's own FAQ blocks automated checks - confirm on a real MSI board"

def windows_scenes():
    s = {}
    s["usb-stick"] = (desk_bg() + [R(70, 80, 150, 48, "deco1", "ink", rx=10, sw=2), R(220, 90, 34, 28, "btn", "ink", rx=3, sw=2), R(228, 98, 6, 4, "ink"), R(240, 98, 6, 4, "ink"), T(145, 109, "8 GB or more", 11, "txt", 700, "middle", 140)],
        "A USB stick with '8 GB or more' written on it.", "Generic drawing")
    s["restart"] = (desk_bg() + [R(70, 30, 180, 140, "bg", "ink", rx=6), R(70, 150, 180, 20, "bar", None, rx=6), C(230, 160, 6, "bg", "ink", 1.2), P("M230 155 v5 M226.5 157 a5 5 0 1 0 7 0", None, "txt", 1.2), R(150, 92, 92, 52, "bg", "ink", rx=4, sw=1), T(160, 106, "Sleep", 6.4, "txt"), T(160, 120, "Shut down", 6.4, "txt"), R(152, 126, 88, 14, "sel", None, rx=2), T(160, 136, "Restart", 6.4, "txt", 700)] + ring(152, 126, 88, 14),
        "The Windows Start menu's power button with its menu open and Restart highlighted.", WIN)
    for key, label in [("f12", "F12"), ("f11", "F11"), ("f10", "F10"), ("f8", "F8"), ("esc", "Esc"), ("esc-f9", "Esc then F9")]:
        keys = ["Esc", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"]
        hot = {"f12": ["F12"], "f11": ["F11"], "f10": ["F10"], "f8": ["F8"], "esc": ["Esc"], "esc-f9": ["Esc", "F9"]}[key]
        els = desk_bg() + [R(10, 64, 300, 72, "bar", "ink", rx=8)]
        for i, k in enumerate(keys):
            x = 16 + i * 22.6
            on = k in hot
            els += [R(x, 82, 20, 20, "acc" if on else "bg", "ink", rx=3, sw=1.2), T(x + 10, 95, k, 6 if len(k) < 3 else 5.4, "acctxt" if on else "txt", 700 if on else 400, "middle", 20)]
            if on: els += ring(x, 82, 20, 20, pad=2.5)
        els.append(T(160, 124, f"Press {label} again and again as the PC starts", 7, "txt", 600, "middle", 280))
        s[f"boot-key-{key}"] = (els, f"The top row of a keyboard with the {label} key highlighted.", "Generic drawing")
    # Desktop keyboards: Delete (startup settings on most motherboards) sits right of the F keys.
    keys = ["F9", "F10", "F11", "F12", "PrtSc", "Insert", "Delete"]
    els = desk_bg() + [R(10, 64, 300, 72, "bar", "ink", rx=8)]
    for i, k in enumerate(keys):
        x = 18 + i * 41.5
        on = k == "Delete"
        els += [R(x, 82, 36, 20, "acc" if on else "bg", "ink", rx=3, sw=1.2), T(x + 18, 95, k, 6.2, "acctxt" if on else "txt", 700 if on else 400, "middle", 34)]
        if on: els += ring(x, 82, 36, 20, pad=2.5)
    els.append(T(160, 124, "Press Delete again and again as the PC starts", 7, "txt", 600, "middle", 280))
    s["boot-key-delete"] = (els, "The top right of a keyboard with the Delete key highlighted.", "Generic drawing")
    s["boot-key-surface"] = (desk_bg() + [R(70, 50, 180, 110, "bg", "ink", rx=10, sw=2), R(80, 60, 160, 90, "bar", None, rx=4), R(108, 44, 22, 6, "acc", "ink", rx=2, sw=1), R(140, 44, 22, 6, "btn", "ink", rx=2, sw=1)] + ring(108, 44, 22, 6, pad=2) + [T(119, 38, "Volume down", 6, "txt", 700, "middle", 60), T(151, 30, "Power", 6, "txt", 400, "middle", 40), T(160, 180, "Hold volume down, then press power", 7, "txt", 600, "middle", 260)],
        "A Surface tablet's top edge with the volume-down button highlighted next to the power button.", "Microsoft: Boot Surface from a USB device")
    s["boot-menu"] = (desk_bg() + [R(20, 20, 280, 160, "blue", "ink", rx=4), T(160, 40, "Boot Menu", 9, "bluetxt", 700, "middle", 200), R(50, 54, 220, 14, "blue", "bluetxt", sw=0.8), T(58, 64, "Windows Boot Manager", 6.4, "bluetxt"), R(50, 72, 220, 14, "sel"), T(58, 82, "UEFI: USB Stick", 6.4, "txt", 700)] + ring(50, 72, 220, 14) + [T(160, 168, "Names differ between PCs. Pick the one with USB.", 6, "bluetxt", 400, "middle", 260)],
        "A startup (boot) menu listing Windows Boot Manager and a USB stick, with the USB stick highlighted.", "Generic drawing; every PC maker's menu looks different")
    s["recovery-11"] = (win_settings(True, ["Home", "*System", "Bluetooth & devices", "Network & internet", "Personalization", "Apps", "Accounts"], "System > Recovery",
                                     [("Reset this PC", None, "Reset PC"), ("Go back", None, "Go back"), ("Advanced startup", "Restart from a disc or USB drive", "Restart now")]),
        "Windows 11 Settings on System, Recovery, showing Reset this PC, Go back and Advanced startup with a Restart now button.", WIN)
    s["recovery-10"] = (win_settings(False, ["Home", "System", "Devices", "Network & Internet", "Apps", "*Update & Security", "Accounts"], "Recovery",
                                     [("Reset this PC", None, "Get started"), ("Advanced startup", "Start up from a device or disc (such as a USB drive)", "Restart now")]),
        "Windows 10 Settings on Update and Security, Recovery, showing Reset this PC and Advanced startup with a Restart now button.", WIN)
    s["advanced-startup"] = (win_settings(True, ["Home", "*System", "Bluetooth & devices", "Network & internet"], "System > Recovery",
                                          [("Advanced startup", "Restart from a disc or USB drive", "Restart now")], target_row=0),
        "The Advanced startup row in Windows Settings with its Restart now button highlighted.", WIN)
    s["use-a-device"] = (desk_bg() + [R(10, 10, 300, 180, "blue")] + [T(30, 36, "Choose an option", 11, "bluetxt", 400)] + flatten([[R(30 + (i % 2) * 130, 52 + (i // 2) * 34, 120, 28, "blue", "bluetxt", sw=0.6), T(56 + (i % 2) * 130, 70 + (i // 2) * 34, t, 7, "bluetxt", 600)] for i, t in enumerate(["Continue", "Use a device", "Troubleshoot", "Turn off your PC"])]) + ring(160, 52, 120, 28),
        "Windows' blue Choose an option screen with Continue, Use a device, Troubleshoot and Turn off your PC, and Use a device highlighted.", WIN)
    # After "Use a device": Windows lists the firmware's boot entries as tiles. Names come from the
    # PC's firmware and the stick, so the caption says to pick the one with USB or the stick's name.
    devices = ["Windows Boot Manager", "UEFI: USB stick", "EFI Network"]
    s["use-a-device-list"] = (desk_bg() + [R(10, 10, 300, 180, "blue"), T(30, 36, "Use a device", 11, "bluetxt", 400)]
        + flatten([[R(30, 52 + i * 30, 160, 24, "sel" if i == 1 else "blue", "bluetxt", sw=0.6), T(52, 67 + i * 30, t, 7, "txt" if i == 1 else "bluetxt", 700 if i == 1 else 600)] for i, t in enumerate(devices)])
        + ring(30, 82, 160, 24) + [T(160, 176, "Names differ between PCs. Pick the one with USB or your stick's name.", 6, "bluetxt", 400, "middle", 270)],
        "Windows' blue Use a device screen listing Windows Boot Manager, UEFI: USB stick and EFI Network, with the USB stick highlighted.", WIN + "; the entries' names come from each PC's firmware")
    s["bitlocker-panel"] = (gtk_window("BitLocker Drive Encryption", [T(26, 44, "BitLocker Drive Encryption", 9, "txt", 700), T(26, 62, "Operating system drive", 7, "txt", 600), T(26, 78, "C: BitLocker on", 7, "txt", 700), T(170, 92, "Suspend protection", 6, "acc"), T(170, 104, "Back up your recovery key", 6, "acc"), T(170, 116, "Turn off BitLocker", 6, "acc", 700)] + ring(168, 109, 70, 9)),
        "Control Panel's BitLocker Drive Encryption page for drive C with links including Back up your recovery key and Turn off BitLocker, Turn off BitLocker highlighted.", "Ubuntu tutorial's Windows BitLocker screenshots")
    s["bitlocker-turn-off"] = (dialog("BitLocker Drive Encryption", ["Turn off BitLocker", "Your drive will be decrypted. This might", "take a long time."], [("Turn off BitLocker", "btn"), ("Cancel", "btn")], 0, icon="bold"),
        "The Turn off BitLocker box with Turn off BitLocker and Cancel buttons, Turn off BitLocker highlighted.", "Ubuntu tutorial's Windows BitLocker screenshots")
    s["encryption-wait"] = (dialog("BitLocker Drive Encryption", ["Decrypting...", "Drive C: 75% Completed"], [("Close", "btn")], -1, icon="bold") + [R(56, 106, 200, 8, "bar", "ink", rx=2, sw=1), R(57, 107, 150, 6, "deco2")],
        "A Decrypting progress box showing drive C three-quarters done.", "Ubuntu tutorial's Windows BitLocker screenshots")
    s["device-encryption-11"] = (win_settings(True, ["Home", "System", "Apps", "Accounts", "*Privacy & security"], "Privacy & security > Device encryption",
                                              [("Device encryption", "Helps protect your data", "On")], target_row=0),
        "Windows 11 Settings on Privacy and security, Device encryption, with its switch.", WIN)
    s["device-encryption-10"] = (win_settings(False, ["Home", "System", "Apps", "*Update & Security"], "Device encryption",
                                              [("Device encryption", "Helps protect your data", "Turn off")], target_row=0),
        "Windows 10 Settings on Update and Security, Device encryption, with a Turn off button.", WIN)
    s["device-encryption-off"] = (dialog("Turn off device encryption?", ["Your drive will be decrypted.", "This can take a while."], [("Turn off", "btn"), ("Cancel", "btn")], 0),
        "A box asking 'Turn off device encryption?' with Turn off and Cancel buttons, Turn off highlighted.", WIN)
    msi = "Settings > Security > Secure Boot"
    s["bios-msi-secure-boot"] = (startup_settings(msi, [("Secure Boot", "Enabled"), ("Secure Boot Mode", "Standard"), ("Key Management", "")], 0),
        "A startup settings screen with the path Settings > Security > Secure Boot at the top and the Secure Boot setting (currently Enabled) highlighted.", BIOS_MSI)
    s["bios-msi-secure-boot-off"] = (startup_settings(msi, [("Secure Boot", "Enabled"), ("Secure Boot Mode", "Standard")], 0, (["Enabled", "Disabled"], 1)),
        "The Secure Boot setting's list open, with Disabled highlighted. The bottom says F10: Save and restart.", BIOS_MSI)
    return s

def driver_scenes():
    s = {}
    tabs = ["Ubuntu Software", "Other Software", "Updates", "Authentication", "Additional Drivers"]
    def sw_tabs(target_tab):
        els = []
        x = 20
        for t in tabs:
            w = len(t) * 3.3 + 10
            on = t == "Additional Drivers"
            els += [T(x + w / 2, 33, t, 5.2, "txt", 700 if on else 400, "middle", w)]
            if on: els.append(R(x, 36, w, 1.6, "acc"))
            if on and target_tab: els += ring(x, 26, w, 11, pad=2)
            x += w + 2
        return els
    s["ubuntu-gpu-1"] = (gtk_window("Software & Updates", sw_tabs(True) + [R(20, 44, 280, 100, "bg", "ink", rx=2, sw=1), T(28, 58, "NVIDIA Corporation: your graphics card", 6, "txt", 700)]),
        "The Software & Updates window with its tabs, and the Additional Drivers tab highlighted.", "Software & Updates layout from Ubuntu 24.04 (reference screenshots)")
    s["ubuntu-gpu-2"] = (gtk_window("Software & Updates", sw_tabs(False) + [R(20, 44, 280, 100, "bg", "ink", rx=2, sw=1), T(28, 58, "NVIDIA Corporation: your graphics card", 6, "txt", 700)]
                         + radio(28, 64, "NVIDIA driver from nvidia-driver (proprietary, tested)", on=True, box=False, size=5.4, target=True, w=230) + radio(28, 82, "NVIDIA Server driver (proprietary)", box=False, size=5.4) + radio(28, 100, "X.Org X server - Nouveau display driver (open source)", box=False, size=5.4)
                         + button(198, 150, 36, "Revert", size=5.4) + button(238, 150, 58, "Apply Changes", target=True, size=5.4)),
        "The Additional Drivers tab listing NVIDIA drivers, with the one marked 'proprietary, tested' chosen and the Apply Changes button highlighted.", "Software & Updates layout from Ubuntu 24.04 (reference screenshots)")
    s["mint-gpu-1"] = (desk_bg() + [R(0, 0, W, H, "hdr"), R(10, 60, 170, 120, "bar", "ink", rx=4), R(18, 68, 154, 14, "bg", "ink", rx=2, sw=1), T(22, 78, "driver", 6.4, "txt"), R(18, 88, 154, 16, "sel", None, rx=2), T(24, 99, "Driver Manager", 6.6, "txt", 700)] + ring(18, 88, 154, 16) + [R(0, 186, W, 14, "bar"), C(10, 193, 5, "acc")],
        "The Linux Mint menu with 'driver' typed in the search box and Driver Manager highlighted.", MINT)
    s["mint-gpu-2"] = (gtk_window("Driver Manager", [T(28, 46, "NVIDIA Corporation: your graphics card", 6, "txt", 700)]
                       + radio(28, 54, "nvidia-driver (recommended)", "NVIDIA driver metapackage", on=True, box=False, size=5.8, target=True, w=200) + radio(28, 82, "xserver-xorg-video-nouveau (open-source)", box=False, size=5.6)
                       + button(198, 166, 36, "Revert", size=5.4) + button(238, 166, 58, "Apply Changes", target=True, size=5.4), subtitle="Additional and proprietary drivers"),
        "Linux Mint's Driver Manager with the NVIDIA driver marked 'recommended' chosen and the Apply Changes button highlighted.", MINT + " (Hardware drivers page)")
    restart_linux = (desk_bg() + [R(170, 20, 130, 90, "bar", "ink", rx=6), T(182, 40, "Power Off", 6.6, "txt"), R(176, 48, 118, 16, "sel", None, rx=3), T(182, 59, "Restart", 6.6, "txt", 700), T(182, 80, "Log Out", 6.6, "txt")] + ring(176, 48, 118, 16),
                     "A Linux power menu with Restart highlighted.", "Generic drawing")
    s["ubuntu-gpu-3"] = s["mint-gpu-3"] = s["ubuntu-wifi-3"] = s["mint-wifi-3"] = restart_linux
    s["ubuntu-wifi-1"] = s["mint-wifi-1"] = s["pop-os-wifi-1"] = (desk_bg() + [R(40, 70, 120, 70, "bg", "ink", rx=5), R(30, 140, 140, 8, "bar", "ink", rx=2), P("M160 120 q30 0 40 20 q6 12 30 12", None, "ink", 2), R(228, 120, 30, 50, "btn", "ink", rx=5), T(100, 180, "Network cable, or USB from your phone", 7, "txt", 600, "middle", 240)],
        "A laptop connected to the internet with a network cable or a phone's USB cable.", "Generic drawing")
    s["ubuntu-wifi-2"] = (gtk_window("Software & Updates", sw_tabs(False) + [R(20, 44, 280, 100, "bg", "ink", rx=2, sw=1), T(28, 58, "Broadcom Inc.: your Wi-Fi", 6, "txt", 700)] + radio(28, 64, "Broadcom 802.11 Linux STA wireless driver (proprietary)", on=True, box=False, size=5.4, target=True, w=230) + radio(28, 82, "Do not use the device", box=False, size=5.4) + button(238, 150, 58, "Apply Changes", target=True, size=5.4)),
        "The Additional Drivers tab with the Broadcom wireless driver chosen and Apply Changes highlighted.", "Software & Updates layout from Ubuntu 24.04 (reference screenshots)")
    s["mint-wifi-2"] = (gtk_window("Driver Manager", [T(28, 46, "Broadcom Inc.: your Wi-Fi", 6, "txt", 700)] + radio(28, 54, "bcmwl-kernel-source (recommended)", "Broadcom 802.11 Linux STA driver", on=True, box=False, size=5.8, target=True, w=200) + button(238, 166, 58, "Apply Changes", target=True, size=5.4), subtitle="Additional and proprietary drivers"),
        "Linux Mint's Driver Manager with the Broadcom driver chosen and Apply Changes highlighted.", MINT + " (Hardware drivers page)")
    terminal = (desk_bg() + [R(30, 30, 260, 140, "hdr", "ink", rx=6), T(42, 52, "$ sudo apt install ...", 7, "hdrtxt"), T(42, 66, "[sudo] password:", 7, "hdrtxt"), R(42, 74, 6, 9, "deco1"), T(160, 156, "Type the command, press Enter, then your password", 6.2, "hdrtxt", 400, "middle", 240)],
                "A terminal window with a command typed and the password prompt.", "Generic drawing")
    s["pop-os-gpu-2"] = s["pop-os-wifi-2"] = terminal
    s["pop-os-gpu-1"] = (desk_bg() + [R(40, 30, 240, 140, "bg", "ink", rx=6), T(160, 56, "Download Pop!_OS", 9, "txt", 700, "middle", 200)] + button(70, 80, 80, "Download") + button(170, 80, 80, "Download (NVIDIA)", "pri", target=True) + [T(160, 130, "Pick the download made for NVIDIA graphics", 6.4, "txt", 400, "middle", 220)],
        "The Pop!_OS download page with a normal Download button and a Download (NVIDIA) button, the NVIDIA one highlighted.", "System76 Pop!_OS download page layout")
    return s

SCENES = {**ubuntu_scenes(), **mint_scenes(), **pop_scenes(), **windows_scenes(), **driver_scenes()}

# ---- Output: SVG ------------------------------------------------------------------------------

def svg(elements, alt):
    css_l = "".join(f".f-{k}{{fill:{v[0]}}}.s-{k}{{stroke:{v[0]}}}" for k, v in COLOURS.items())
    css_d = "".join(f".f-{k}{{fill:{v[1]}}}.s-{k}{{stroke:{v[1]}}}" for k, v in COLOURS.items())
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" role="img" aria-label="{escape(alt, {chr(34): "&quot;"})}">',
           f'<title>{escape(alt)}</title>',
           f'<style>{css_l}@media (prefers-color-scheme: dark){{{css_d}}}@media print{{{css_l}}}text{{font-family:Lexend,"Segoe UI",system-ui,sans-serif}}</style>']
    for e in elements:
        if e["t"] == "rect":
            a = f'x="{e["x"]:.1f}" y="{e["y"]:.1f}" width="{e["w"]:.1f}" height="{e["h"]:.1f}" rx="{e["rx"]}"'
            out.append(f'<rect {a} {paint(e)}/>')
        elif e["t"] == "circle":
            out.append(f'<circle cx="{e["cx"]:.1f}" cy="{e["cy"]:.1f}" r="{e["r"]:.1f}" {paint(e)}/>')
        elif e["t"] == "path":
            out.append(f'<path d="{e["d"]}" {paint(e)} stroke-linecap="round" stroke-linejoin="round"/>')
        elif e["t"] == "text":
            anchor = {"start": "start", "middle": "middle", "end": "end"}[e["anchor"]]
            out.append(f'<text x="{e["x"]:.1f}" y="{e["y"]:.1f}" font-size="{e["size"]}" font-weight="{e["weight"]}" text-anchor="{anchor}" class="f-{e["cls"]}">{escape(e["s"])}</text>')
    out.append("</svg>")
    return "\n".join(out)

def paint(e):
    cls = []
    cls.append(f'f-{e["fill"]}' if e.get("fill") else "")
    cls.append(f's-{e["stroke"]}' if e.get("stroke") else "")
    fill = "" if e.get("fill") else ' fill="none"'
    sw = f' stroke-width="{e["sw"]}"' if e.get("stroke") else ""
    dash = f' stroke-dasharray="{e["dash"]}"' if e.get("dash") else ""
    return f'class="{" ".join(c for c in cls if c)}"{fill}{sw}{dash}'

# ---- Output: XAML -----------------------------------------------------------------------------

def xaml(elements):
    out = [f'<Canvas Width="{W}" Height="{H}">']
    def brush(k): return f'{{DynamicResource GH.Ill.{k}}}'
    for e in elements:
        if e["t"] == "rect":
            attrs = f'Canvas.Left="{e["x"]:.1f}" Canvas.Top="{e["y"]:.1f}" Width="{e["w"]:.1f}" Height="{e["h"]:.1f}" RadiusX="{e["rx"]}" RadiusY="{e["rx"]}"'
            out.append(f'<Rectangle {attrs}{xpaint(e, brush)} />')
        elif e["t"] == "circle":
            r = e["r"]
            out.append(f'<Ellipse Canvas.Left="{e["cx"]-r:.1f}" Canvas.Top="{e["cy"]-r:.1f}" Width="{2*r:.1f}" Height="{2*r:.1f}"{xpaint(e, brush)} />')
        elif e["t"] == "path":
            out.append(f'<Path Data="{e["d"]}"{xpaint(e, brush)} StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round" />')
        elif e["t"] == "text":
            size = e["size"]
            width = e["width"] or (len(e["s"]) * size * 0.62 + 4)
            left = {"start": e["x"], "middle": e["x"] - width / 2, "end": e["x"] - width}[e["anchor"]]
            align = {"start": "Left", "middle": "Center", "end": "Right"}[e["anchor"]]
            weight = "SemiBold" if e["weight"] >= 600 else "Normal"
            out.append(f'<TextBlock Canvas.Left="{left:.1f}" Canvas.Top="{e["y"]-size*0.95:.1f}" Width="{width:.1f}" TextAlignment="{align}" FontSize="{size}" FontWeight="{weight}" '
                       f'FontFamily="{{DynamicResource BodyFont}}" Foreground="{brush(e["cls"])}" Text="{escape(e["s"], {chr(34): "&quot;"})}" />')
    out.append("</Canvas>")
    return "\n".join(out)

def xpaint(e, brush):
    s = ""
    if e.get("fill"): s += f' Fill="{brush(e["fill"])}"'
    if e.get("stroke"): s += f' Stroke="{brush(e["stroke"])}" StrokeThickness="{e["sw"]}"'
    if e.get("dash"): s += f' StrokeDashArray="{e["dash"]}"'
    return s

def overflow(pid, els):
    """Flags text that would run outside the picture (approximate Lexend widths)."""
    problems = []
    for e in els:
        if e["t"] != "text": continue
        w = len(e["s"]) * e["size"] * (0.58 if e["weight"] >= 600 else 0.54)
        left = {"start": e["x"], "middle": e["x"] - w / 2, "end": e["x"] - w}[e["anchor"]]
        if left < 4 or left + w > W - 4:
            problems.append(f"{pid}: '{e['s']}' runs off the picture ({left:.0f}..{left + w:.0f})")
    return problems

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for f in OUT.glob("*.svg"): f.unlink()
    catalog = {}
    templates = []
    problems = []
    for pid, (els, alt, source) in sorted(SCENES.items()):
        els = flatten(els)
        problems += overflow(pid, els)
        (OUT / f"{pid}.svg").write_text(svg(els, alt))
        catalog[pid] = {"alt": alt, "checkedAgainst": source}
        templates.append(f'    <ControlTemplate x:Key="pic.{pid}" TargetType="ContentControl">\n        <Viewbox Stretch="Uniform">\n{indent(xaml(els), 12)}\n        </Viewbox>\n    </ControlTemplate>')
    (OUT / "catalog.json").write_text(json.dumps(catalog, indent=1, ensure_ascii=False) + "\n")
    (ROOT / "src/GreyHaven.App/Theming/Illustrations.xaml").write_text(
        '<!-- Generated by tools/illustrations/build.py. Do not edit; change the generator. -->\n'
        '<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"\n'
        '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">\n' + "\n".join(templates) + "\n</ResourceDictionary>\n")
    (ROOT / "src/GreyHaven.App/Theming/IllustrationColours.g.cs").write_text(
        "// Generated by tools/illustrations/build.py. Do not edit.\n"
        "namespace GreyHaven.App.Theming;\n\n"
        "/// <summary>Light and dark colours for the walkthrough pictures (GH.Ill.* brushes).</summary>\n"
        "internal static class IllustrationColours\n{\n"
        "    public static readonly IReadOnlyDictionary<string, (string Light, string Dark)> All = new Dictionary<string, (string, string)>\n    {\n"
        + "".join(f'        ["{k}"] = ("{v[0]}", "{v[1]}"),\n' for k, v in COLOURS.items()) + "    };\n}\n")
    print(f"{len(SCENES)} pictures")
    if problems:
        raise SystemExit("Text runs off pictures:\n  " + "\n  ".join(problems))

def indent(s, n):
    return "\n".join(" " * n + line for line in s.splitlines())

if __name__ == "__main__":
    main()
